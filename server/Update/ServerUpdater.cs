using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DischeeseServer.Update;

internal static class ServerUpdater
{
    public const string ApplyUpdateArgument = "--apply-update";

    private const string ReleasesUrl =
        "https://api.github.com/repos/Pro203S/chzzk-rpc/releases?per_page=100";

    private const string UpdateTargetPrefix = "--update-target=";
    private const string UpdateDirectoryPrefix = "--update-directory=";
    private const string WaitForProcessPrefix = "--wait-for-process=";
    private const string RestartPortPrefix = "--restart-port=";
    private const string CleanupDirectoryPrefix =
        "--cleanup-update-directory=";
    private const string CleanupWaitProcessPrefix =
        "--cleanup-wait-process=";

    private const long MaximumDownloadSize = 256L * 1024 * 1024;
    private const long MaximumExtractedSize = 512L * 1024 * 1024;

    private static readonly SemaphoreSlim UpdateLock = new(1, 1);
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static bool updateStarted;
    private static DateTimeOffset retryAfter;
    private static string? previousFailure;

    private static string UpdatesRoot => Path.Combine(
        Logger.DirectoryPath,
        "updates"
    );

    public static bool IsApplyUpdate(IReadOnlyList<string> arguments)
    {
        return arguments.Contains(ApplyUpdateArgument);
    }

    public static bool IsProtocolVersion(string value)
    {
        if (
            value.Length < 6 ||
            value[0] != 'v'
        )
        {
            return false;
        }

        string[] parts = value[1..].Split('.');

        return parts.Length == 3 &&
            parts.All(part =>
                part.Length > 0 &&
                part.All(char.IsAsciiDigit)
            );
    }

    public static async Task StartUpdateAsync(
        string targetVersion,
        int restartPort,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsProtocolVersion(targetVersion))
        {
            throw new ArgumentException(
                "올바르지 않은 프로토콜 버전입니다.",
                nameof(targetVersion)
            );
        }

        await UpdateLock.WaitAsync(cancellationToken);

        bool attempted = false;

        try
        {
            if (updateStarted)
            {
                return;
            }

            if (DateTimeOffset.UtcNow < retryAfter)
            {
                throw new InvalidOperationException(
                    "자동 업데이트를 잠시 후 다시 시도합니다. " +
                    previousFailure
                );
            }

            attempted = true;

            ApplicationCommand currentCommand =
                ApplicationCommand.GetCurrent();

            if (currentCommand.UsesDotNetHost)
            {
                throw new InvalidOperationException(
                    "개발용 dotnet 실행에서는 자동 업데이트를 사용할 수 없습니다."
                );
            }

            ReleaseAsset asset = await FindReleaseAssetAsync(
                targetVersion,
                cancellationToken
            );

            string updateDirectory = CreateUpdateDirectory();

            try
            {
                string archivePath = Path.Combine(
                    updateDirectory,
                    asset.Name
                );

                await DownloadAssetAsync(
                    asset,
                    archivePath,
                    cancellationToken
                );

                string updaterPath = await ExtractUpdaterAsync(
                    archivePath,
                    updateDirectory,
                    cancellationToken
                );

                ApplicationCommand updaterCommand = new(updaterPath, []);

                BackgroundProcess.Start(
                    updaterCommand,
                    [
                        ApplyUpdateArgument,
                        UpdateTargetPrefix + currentCommand.ApplicationPath,
                        UpdateDirectoryPrefix + updateDirectory,
                        WaitForProcessPrefix + Environment.ProcessId,
                        RestartPortPrefix + restartPort
                    ]
                );

                updateStarted = true;
            }
            catch
            {
                TryDeleteDirectory(updateDirectory);
                throw;
            }
        }
        catch (Exception ex) when (attempted)
        {
            retryAfter = DateTimeOffset.UtcNow.AddMinutes(1);
            previousFailure = ex.Message;
            throw;
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    public static async Task ApplyUpdateAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default
    )
    {
        Logger.WriteToConsole = false;

        string targetPath = Path.GetFullPath(
            GetRequiredArgument(arguments, UpdateTargetPrefix)
        );

        string updateDirectory = ValidateUpdateDirectory(
            GetRequiredArgument(arguments, UpdateDirectoryPrefix)
        );

        int processId = GetRequiredIntArgument(
            arguments,
            WaitForProcessPrefix
        );

        int restartPort = GetRequiredIntArgument(
            arguments,
            RestartPortPrefix
        );

        await WaitForProcessExitAsync(processId, cancellationToken);

        string sourcePath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "업데이트 실행 파일 경로를 가져올 수 없습니다."
            );

        string targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException(
                "서버 설치 디렉터리를 가져올 수 없습니다."
            );

        Directory.CreateDirectory(targetDirectory);

        string stagingPath = Path.Combine(
            targetDirectory,
            $".{Path.GetFileName(targetPath)}-{Guid.NewGuid():N}.tmp"
        );

        try
        {
            await CopyFileAsync(
                sourcePath,
                stagingPath,
                cancellationToken
            );

            CopyUnixFileMode(sourcePath, stagingPath);
            File.Move(stagingPath, targetPath, overwrite: true);

            StartUpdatedServer(
                targetPath,
                restartPort,
                updateDirectory
            );
        }
        catch
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }

            TryRestartServer(targetPath, restartPort);
            throw;
        }
    }

    public static async Task CleanupPreviousUpdateAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default
    )
    {
        string? rawDirectory = FindArgument(
            arguments,
            CleanupDirectoryPrefix
        );

        if (rawDirectory is null)
        {
            return;
        }

        Logger.WriteToConsole = false;

        try
        {
            string updateDirectory = ValidateUpdateDirectory(rawDirectory);
            int processId = GetRequiredIntArgument(
                arguments,
                CleanupWaitProcessPrefix
            );

            await WaitForProcessExitAsync(processId, cancellationToken);

            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    if (Directory.Exists(updateDirectory))
                    {
                        Directory.Delete(updateDirectory, recursive: true);
                    }

                    return;
                }
                catch (IOException) when (attempt < 19)
                {
                    await Task.Delay(100, cancellationToken);
                }
                catch (UnauthorizedAccessException) when (attempt < 19)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Failed to clean the previous update directory.");
            Logger.Log(ex.Message);
        }
    }

    private static async Task<ReleaseAsset> FindReleaseAssetAsync(
        string targetVersion,
        CancellationToken cancellationToken
    )
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            ReleasesUrl
        );

        using HttpResponseMessage response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        await using Stream content = await response.Content
            .ReadAsStreamAsync(cancellationToken);

        List<Release>? releases = await JsonSerializer.DeserializeAsync<
            List<Release>
        >(
            content,
            cancellationToken: cancellationToken
        );

        Release release = releases?.FirstOrDefault(item =>
            !item.Draft &&
            string.Equals(
                item.TagName,
                targetVersion,
                StringComparison.OrdinalIgnoreCase
            )
        ) ?? throw new InvalidOperationException(
            $"{targetVersion} 서버 릴리스를 찾을 수 없습니다."
        );

        string runtimeId = GetRuntimeId();
        string expectedName = $"server-{runtimeId}.zip";

        ReleaseAsset[] serverAssets = release.Assets
            .Where(asset =>
                !asset.Name.StartsWith(
                    "discheese-extension",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                asset.Name.EndsWith(
                    ".zip",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                (
                    string.IsNullOrEmpty(asset.State) ||
                    string.Equals(
                        asset.State,
                        "uploaded",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            .ToArray();

        ReleaseAsset? selectedAsset = serverAssets.FirstOrDefault(asset =>
            string.Equals(
                asset.Name,
                expectedName,
                StringComparison.OrdinalIgnoreCase
            )
        );

        selectedAsset ??= serverAssets.SingleOrDefault(asset =>
            asset.Name.Contains(
                runtimeId,
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (selectedAsset is null)
        {
            throw new InvalidOperationException(
                $"{runtimeId} 서버 압축파일을 찾을 수 없습니다."
            );
        }

        if (
            selectedAsset.Size <= 0 ||
            selectedAsset.Size > MaximumDownloadSize
        )
        {
            throw new InvalidOperationException(
                "서버 압축파일 크기가 올바르지 않습니다."
            );
        }

        if (
            !Uri.TryCreate(
                selectedAsset.DownloadUrl,
                UriKind.Absolute,
                out Uri? downloadUri
            ) ||
            downloadUri.Scheme != Uri.UriSchemeHttps
        )
        {
            throw new InvalidOperationException(
                "서버 다운로드 URL이 올바르지 않습니다."
            );
        }

        return selectedAsset;
    }

    private static async Task DownloadAssetAsync(
        ReleaseAsset asset,
        string destinationPath,
        CancellationToken cancellationToken
    )
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(
            asset.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        long? contentLength = response.Content.Headers.ContentLength;

        if (
            contentLength is > MaximumDownloadSize ||
            (contentLength is > 0 && contentLength != asset.Size)
        )
        {
            throw new InvalidOperationException(
                "서버 압축파일 크기가 릴리스 정보와 다릅니다."
            );
        }

        long totalBytes = 0;

        await using (
            Stream source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
        )
        await using (
            FileStream destination = new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            )
        )
        {
            byte[] buffer = new byte[81920];

            while (true)
            {
                int bytesRead = await source.ReadAsync(
                    buffer,
                    cancellationToken
                );

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;

                if (totalBytes > MaximumDownloadSize)
                {
                    throw new InvalidOperationException(
                        "서버 압축파일이 허용 크기를 초과했습니다."
                    );
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken
                );
            }

            await destination.FlushAsync(cancellationToken);
        }

        if (totalBytes != asset.Size)
        {
            throw new InvalidOperationException(
                "다운로드한 서버 압축파일 크기가 올바르지 않습니다."
            );
        }

        await VerifyDigestAsync(
            destinationPath,
            asset.Digest,
            cancellationToken
        );
    }

    private static async Task VerifyDigestAsync(
        string path,
        string? digest,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return;
        }

        const string prefix = "sha256:";

        if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "지원하지 않는 서버 압축파일 digest 형식입니다."
            );
        }

        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(
            stream,
            cancellationToken
        );

        string actualDigest = Convert.ToHexString(hash);
        string expectedDigest = digest[prefix.Length..];

        if (!string.Equals(
            actualDigest,
            expectedDigest,
            StringComparison.OrdinalIgnoreCase
        ))
        {
            throw new InvalidOperationException(
                "서버 압축파일 SHA-256 검증에 실패했습니다."
            );
        }
    }

    private static async Task<string> ExtractUpdaterAsync(
        string archivePath,
        string updateDirectory,
        CancellationToken cancellationToken
    )
    {
        string extractionDirectory = Path.Combine(
            updateDirectory,
            "extracted"
        );

        Directory.CreateDirectory(extractionDirectory);

        string extractionRoot = Path.GetFullPath(extractionDirectory) +
            Path.DirectorySeparatorChar;

        long extractedBytes = 0;

        using ZipArchive archive = ZipFile.OpenRead(archivePath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            extractedBytes += entry.Length;

            if (extractedBytes > MaximumExtractedSize)
            {
                throw new InvalidOperationException(
                    "압축 해제된 서버가 허용 크기를 초과했습니다."
                );
            }

            string destinationPath = Path.GetFullPath(
                Path.Combine(extractionDirectory, entry.FullName)
            );

            if (!destinationPath.StartsWith(
                extractionRoot,
                GetPathComparison()
            ))
            {
                throw new InvalidOperationException(
                    "서버 압축파일에 안전하지 않은 경로가 있습니다."
                );
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            string? destinationParent =
                Path.GetDirectoryName(destinationPath);

            if (destinationParent is not null)
            {
                Directory.CreateDirectory(destinationParent);
            }

            await using Stream source = entry.Open();
            await using FileStream destination = new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            );

            await source.CopyToAsync(destination, cancellationToken);
        }

        string executableName = OperatingSystem.IsWindows()
            ? "Discheese-server.exe"
            : "Discheese-server";

        string[] executablePaths = Directory.GetFiles(
            extractionDirectory,
            executableName,
            SearchOption.AllDirectories
        );

        if (executablePaths.Length != 1)
        {
            throw new InvalidOperationException(
                "서버 압축파일에서 실행 파일을 찾을 수 없습니다."
            );
        }

        string executablePath = executablePaths[0];

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                executablePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute
            );
        }

        return executablePath;
    }

    private static void StartUpdatedServer(
        string targetPath,
        int restartPort,
        string updateDirectory
    )
    {
        BackgroundProcess.Start(
            new ApplicationCommand(targetPath, []),
            [
                $"--port={restartPort}",
                CleanupDirectoryPrefix + updateDirectory,
                CleanupWaitProcessPrefix + Environment.ProcessId
            ]
        );
    }

    private static void TryRestartServer(
        string targetPath,
        int restartPort
    )
    {
        if (!File.Exists(targetPath))
        {
            return;
        }

        try
        {
            BackgroundProcess.Start(
                new ApplicationCommand(targetPath, []),
                [$"--port={restartPort}"]
            );
        }
        catch (Exception ex)
        {
            Logger.Log("Failed to restart the previous server.");
            Logger.Log(ex.Message);
        }
    }

    private static async Task WaitForProcessExitAsync(
        int processId,
        CancellationToken cancellationToken
    )
    {
        Process process;

        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return;
        }

        using (process)
        using (CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            ))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    "이전 서버 프로세스가 종료되지 않았습니다."
                );
            }
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken
    )
    {
        await using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        await source.CopyToAsync(destination, cancellationToken);
    }

    private static void CopyUnixFileMode(
        string sourcePath,
        string destinationPath
    )
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                destinationPath,
                File.GetUnixFileMode(sourcePath)
            );
        }
    }

    private static string CreateUpdateDirectory()
    {
        Directory.CreateDirectory(UpdatesRoot);

        foreach (string path in Directory.EnumerateDirectories(
            UpdatesRoot
        ))
        {
            TryDeleteDirectory(path);
        }

        string updateDirectory = Path.Combine(
            UpdatesRoot,
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(updateDirectory);
        return updateDirectory;
    }

    private static string ValidateUpdateDirectory(string path)
    {
        string root = Path.GetFullPath(UpdatesRoot);
        string candidate = Path.GetFullPath(path);

        if (!string.Equals(
            Path.GetDirectoryName(candidate),
            root,
            GetPathComparison()
        ))
        {
            throw new InvalidOperationException(
                "업데이트 임시 디렉터리 경로가 올바르지 않습니다."
            );
        }

        return candidate;
    }

    private static string GetRequiredArgument(
        IReadOnlyList<string> arguments,
        string prefix
    )
    {
        string? value = FindArgument(arguments, prefix);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"필수 업데이트 인수가 없습니다: {prefix}"
            );
        }

        return value;
    }

    private static int GetRequiredIntArgument(
        IReadOnlyList<string> arguments,
        string prefix
    )
    {
        string rawValue = GetRequiredArgument(arguments, prefix);

        if (!int.TryParse(rawValue, out int value) || value <= 0)
        {
            throw new InvalidOperationException(
                $"업데이트 인수가 올바르지 않습니다: {prefix}"
            );
        }

        return value;
    }

    private static string? FindArgument(
        IReadOnlyList<string> arguments,
        string prefix
    )
    {
        string? argument = arguments.FirstOrDefault(value =>
            value.StartsWith(prefix, StringComparison.Ordinal)
        );

        return argument?[prefix.Length..];
    }

    private static string GetRuntimeId()
    {
        string operatingSystem = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsLinux()
                ? "linux"
                : OperatingSystem.IsMacOS()
                    ? "osx"
                    : throw new PlatformNotSupportedException(
                        "자동 업데이트를 지원하지 않는 운영체제입니다."
                    );

        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                "자동 업데이트를 지원하지 않는 CPU 아키텍처입니다."
            )
        };

        return $"{operatingSystem}-{architecture}";
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Discheese-server", "1.0")
        );

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"
            )
        );

        client.DefaultRequestHeaders.Add(
            "X-GitHub-Api-Version",
            "2022-11-28"
        );

        return client;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private sealed record Release(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("assets")]
        IReadOnlyList<ReleaseAsset> Assets
    );

    private sealed record ReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("browser_download_url")]
        string DownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest
    );
}
