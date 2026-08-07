using System.ComponentModel;
using System.Diagnostics;

namespace DischeeseServer.AutoStart;

internal static class ApplicationInstaller
{
    private const string ApplicationBaseName = "Discheese-server";
    private const string HostedApplicationDirectoryName = "app";

    public static async Task<ApplicationCommand> ReplaceCurrentAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await ReplaceAsync(
            ApplicationCommand.GetCurrent(),
            Logger.DirectoryPath,
            cancellationToken
        );
    }

    internal static async Task<ApplicationCommand> ReplaceAsync(
        ApplicationCommand currentCommand,
        string installationDirectory,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            installationDirectory
        );

        installationDirectory = Path.GetFullPath(
            installationDirectory
        );

        Directory.CreateDirectory(installationDirectory);
        DeleteStaleStagingItems(installationDirectory);

        return currentCommand.UsesDotNetHost
            ? await ReplaceHostedApplicationAsync(
                currentCommand,
                installationDirectory,
                cancellationToken
            )
            : await ReplaceExecutableAsync(
                currentCommand,
                installationDirectory,
                cancellationToken
            );
    }

    private static async Task<ApplicationCommand> ReplaceExecutableAsync(
        ApplicationCommand currentCommand,
        string installationDirectory,
        CancellationToken cancellationToken
    )
    {
        string sourcePath = Path.GetFullPath(
            currentCommand.ApplicationPath
        );

        string installedPath = Path.Combine(
            installationDirectory,
            ApplicationBaseName + Path.GetExtension(sourcePath)
        );

        if (PathsEqual(sourcePath, installedPath))
        {
            DeletePreviousApplicationFiles(
                installationDirectory,
                installedPath
            );
            DeleteHostedApplicationDirectory(installationDirectory);
            return new ApplicationCommand(installedPath, []);
        }

        string stagingPath = Path.Combine(
            installationDirectory,
            $".{ApplicationBaseName}-{Guid.NewGuid():N}.tmp"
        );

        try
        {
            await CopyFileAsync(
                sourcePath,
                stagingPath,
                cancellationToken
            );

            CopyUnixFileMode(sourcePath, stagingPath);
            DeletePreviousApplicationFiles(
                installationDirectory,
                installedPath
            );
            DeleteHostedApplicationDirectory(installationDirectory);
            await StopInstalledExecutableAsync(
                installedPath,
                cancellationToken
            );
            File.Move(stagingPath, installedPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }

        return new ApplicationCommand(installedPath, []);
    }

    private static async Task<ApplicationCommand>
        ReplaceHostedApplicationAsync(
            ApplicationCommand currentCommand,
            string installationDirectory,
            CancellationToken cancellationToken
        )
    {
        string sourceAssemblyPath = Path.GetFullPath(
            currentCommand.ApplicationPath
        );

        string sourceDirectory = Path.GetDirectoryName(sourceAssemblyPath)
            ?? throw new InvalidOperationException(
                "현재 애플리케이션 디렉터리를 가져올 수 없습니다."
            );

        string installedDirectory = Path.Combine(
            installationDirectory,
            HostedApplicationDirectoryName
        );

        string installedAssemblyPath = Path.Combine(
            installedDirectory,
            Path.GetFileName(sourceAssemblyPath)
        );

        if (!PathsEqual(sourceDirectory, installedDirectory))
        {
            string stagingDirectory = Path.Combine(
                installationDirectory,
                $".{HostedApplicationDirectoryName}-{Guid.NewGuid():N}.tmp"
            );

            try
            {
                await CopyDirectoryAsync(
                    sourceDirectory,
                    stagingDirectory,
                    cancellationToken
                );

                DeletePreviousApplicationFiles(
                    installationDirectory,
                    exceptPath: null
                );
                DeleteHostedApplicationDirectory(
                    installationDirectory
                );
                Directory.Move(stagingDirectory, installedDirectory);
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
            }
        }
        else
        {
            DeletePreviousApplicationFiles(
                installationDirectory,
                exceptPath: null
            );
        }

        return new ApplicationCommand(
            currentCommand.FileName,
            [installedAssemblyPath]
        );
    }

    private static async Task CopyDirectoryAsync(
        string sourceDirectory,
        string destinationDirectory,
        CancellationToken cancellationToken
    )
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string sourcePath in Directory.EnumerateFiles(
            sourceDirectory,
            "*",
            SearchOption.AllDirectories
        ))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(
                sourceDirectory,
                sourcePath
            );

            string destinationPath = Path.Combine(
                destinationDirectory,
                relativePath
            );

            string? destinationParent =
                Path.GetDirectoryName(destinationPath);

            if (destinationParent is not null)
            {
                Directory.CreateDirectory(destinationParent);
            }

            await CopyFileAsync(
                sourcePath,
                destinationPath,
                cancellationToken
            );

            CopyUnixFileMode(sourcePath, destinationPath);
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

    private static void DeletePreviousApplicationFiles(
        string installationDirectory,
        string? exceptPath
    )
    {
        foreach (string path in Directory.EnumerateFiles(
            installationDirectory
        ))
        {
            string fileName = Path.GetFileName(path);

            if (
                !fileName.StartsWith(
                    ApplicationBaseName,
                    StringComparison.OrdinalIgnoreCase
                ) ||
                (exceptPath is not null && PathsEqual(path, exceptPath))
            )
            {
                continue;
            }

            File.Delete(path);
        }
    }

    private static void DeleteHostedApplicationDirectory(
        string installationDirectory
    )
    {
        string path = Path.Combine(
            installationDirectory,
            HostedApplicationDirectoryName
        );

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void DeleteStaleStagingItems(
        string installationDirectory
    )
    {
        foreach (string path in Directory.EnumerateFiles(
            installationDirectory,
            $".{ApplicationBaseName}-*.tmp"
        ))
        {
            File.Delete(path);
        }

        foreach (string path in Directory.EnumerateDirectories(
            installationDirectory,
            $".{HostedApplicationDirectoryName}-*.tmp"
        ))
        {
            Directory.Delete(path, recursive: true);
        }
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

    private static async Task StopInstalledExecutableAsync(
        string installedPath,
        CancellationToken cancellationToken
    )
    {
        if (!File.Exists(installedPath))
        {
            return;
        }

        string processName = Path.GetFileNameWithoutExtension(
            installedPath
        );

        foreach (Process process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                string? processPath;

                try
                {
                    processPath = process.MainModule?.FileName;
                }
                catch (Exception ex) when (
                    ex is Win32Exception or
                        InvalidOperationException or
                        NotSupportedException
                )
                {
                    continue;
                }

                if (
                    processPath is null ||
                    !PathsEqual(processPath, installedPath)
                )
                {
                    continue;
                }

                Logger.Log(
                    "Stopping the previously installed server " +
                    $"(PID: {process.Id})..."
                );

                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
            }
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            comparison
        );
    }
}
