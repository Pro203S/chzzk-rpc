using System.Diagnostics;
using System.Security;
using System.Text;

namespace DischeeseServer.AutoStart;

public sealed class MacOsAutoStart
{
    private readonly string applicationId;

    public MacOsAutoStart(string applicationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);

        this.applicationId = applicationId;
    }

    private string LaunchAgentsDirectory => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile
        ),
        "Library",
        "LaunchAgents"
    );

    private string PlistPath => Path.Combine(
        LaunchAgentsDirectory,
        $"{applicationId}.plist"
    );

    public async Task EnsureEnabledAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        ApplicationCommand command = ApplicationCommand
            .GetCurrent()
            .AppendArgument("--background");

        string desiredPlist = CreatePlist(command);

        bool requiresRegistration =
            !File.Exists(PlistPath) ||
            !string.Equals(
                await File.ReadAllTextAsync(
                    PlistPath,
                    cancellationToken
                ),
                desiredPlist,
                StringComparison.Ordinal
            );

        if (requiresRegistration)
        {
            await RegisterAsync(
                desiredPlist,
                cancellationToken
            );
        }
    }

    public async Task DisableAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        int userId = await GetUserIdAsync(cancellationToken);

        await RunProcessAsync(
            "/bin/launchctl",
            [
                "bootout",
                $"gui/{userId}/{applicationId}"
            ],
            cancellationToken,
            ignoreExitCode: true
        );

        if (File.Exists(PlistPath))
        {
            File.Delete(PlistPath);
        }
    }

    private async Task RegisterAsync(
        string plist,
        CancellationToken cancellationToken
    )
    {
        int userId = await GetUserIdAsync(cancellationToken);

        Directory.CreateDirectory(LaunchAgentsDirectory);

        await RunProcessAsync(
            "/bin/launchctl",
            [
                "bootout",
                $"gui/{userId}/{applicationId}"
            ],
            cancellationToken,
            ignoreExitCode: true
        );

        await File.WriteAllTextAsync(
            PlistPath,
            plist,
            new UTF8Encoding(false),
            cancellationToken
        );

        await RunProcessAsync(
            "/bin/launchctl",
            [
                "bootstrap",
                $"gui/{userId}",
                PlistPath
            ],
            cancellationToken
        );
    }

    private string CreatePlist(ApplicationCommand command)
    {
        string homeDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile
        );

        string logDirectory = Path.Combine(
            homeDirectory,
            "Library",
            "Logs",
            applicationId
        );

        Directory.CreateDirectory(logDirectory);

        string workingDirectory =
            Path.GetDirectoryName(command.ApplicationPath)
            ?? homeDirectory;

        string programArguments = string.Join(
            Environment.NewLine,
            new[] { command.FileName }
                .Concat(command.Arguments)
                .Select(argument =>
                    $"            <string>{EscapeXml(argument)}</string>"
                )
        );

        return $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC
            "-//Apple//DTD PLIST 1.0//EN"
            "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>Label</key>
            <string>{{EscapeXml(applicationId)}}</string>

            <key>ProgramArguments</key>
            <array>
        {{programArguments}}
            </array>

            <key>RunAtLoad</key>
            <true/>

            <key>KeepAlive</key>
            <true/>

            <key>ProcessType</key>
            <string>Background</string>

            <key>WorkingDirectory</key>
            <string>{{EscapeXml(workingDirectory)}}</string>

            <key>StandardOutPath</key>
            <string>{{EscapeXml(Path.Combine(logDirectory, "stdout.log"))}}</string>

            <key>StandardErrorPath</key>
            <string>{{EscapeXml(Path.Combine(logDirectory, "stderr.log"))}}</string>
        </dict>
        </plist>
        """;
    }

    private static async Task<int> GetUserIdAsync(
        CancellationToken cancellationToken
    )
    {
        ProcessResult result = await RunProcessAsync(
            "/usr/bin/id",
            ["-u"],
            cancellationToken
        );

        if (!int.TryParse(result.StandardOutput.Trim(), out int userId))
        {
            throw new InvalidOperationException(
                "현재 사용자의 UID를 가져오지 못했습니다."
            );
        }

        return userId;
    }

    private static string EscapeXml(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool ignoreExitCode = false
    )
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        Task<string> standardOutputTask =
            process.StandardOutput.ReadToEndAsync(cancellationToken);

        Task<string> standardErrorTask =
            process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;

        if (!ignoreExitCode && process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{fileName} 실행 실패\n" +
                $"Exit code: {process.ExitCode}\n" +
                $"stderr: {standardError}"
            );
        }

        return new ProcessResult(
            process.ExitCode,
            standardOutput,
            standardError
        );
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError
    );
}
