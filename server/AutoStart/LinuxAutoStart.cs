using System.Diagnostics;
using System.Text;

namespace DischeeseServer.AutoStart;

public sealed class LinuxAutoStart
{
    private readonly string serviceName;

    public LinuxAutoStart(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        this.serviceName = serviceName.EndsWith(
            ".service",
            StringComparison.Ordinal
        )
            ? serviceName
            : $"{serviceName}.service";
    }

    private string ServiceDirectory => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile
        ),
        ".config",
        "systemd",
        "user"
    );

    private string ServicePath => Path.Combine(
        ServiceDirectory,
        serviceName
    );

    public async Task EnsureEnabledAsync(
        CancellationToken cancellationToken = default
    )
    {
        ApplicationCommand command = ApplicationCommand
            .GetCurrent()
            .AppendArgument(BackgroundProcess.Argument);

        await EnsureEnabledAsync(command, cancellationToken);
    }

    internal async Task EnsureEnabledAsync(
        ApplicationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string desiredService = CreateService(command);

        await RunSystemctlAsync(
            ["--user", "disable", serviceName],
            cancellationToken,
            ignoreExitCode: true
        );

        Directory.CreateDirectory(ServiceDirectory);

        await File.WriteAllTextAsync(
            ServicePath,
            desiredService,
            new UTF8Encoding(false),
            cancellationToken
        );

        await RunSystemctlAsync(
            ["--user", "daemon-reload"],
            cancellationToken
        );

        await RunSystemctlAsync(
            ["--user", "enable", serviceName],
            cancellationToken
        );
    }

    public async Task DisableAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        await RunSystemctlAsync(
            ["--user", "disable", "--now", serviceName],
            cancellationToken,
            ignoreExitCode: true
        );

        if (File.Exists(ServicePath))
        {
            File.Delete(ServicePath);
        }

        await RunSystemctlAsync(
            ["--user", "daemon-reload"],
            cancellationToken,
            ignoreExitCode: true
        );
    }

    private string CreateService(ApplicationCommand command)
    {
        string workingDirectory =
            Path.GetDirectoryName(command.ApplicationPath)
            ?? Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile
            );

        string execStart = string.Join(
            " ",
            new[] { command.FileName }
                .Concat(command.Arguments)
                .Select(EscapeSystemdArgument)
        );

        return $"""
        [Unit]
        Description=Discheese server
        Wants=network-online.target
        After=network-online.target

        [Service]
        Type=simple
        ExecStart={execStart}
        WorkingDirectory={EscapeSystemdArgument(workingDirectory)}
        Restart=on-failure
        RestartSec=5
        StandardOutput=journal
        StandardError=journal

        [Install]
        WantedBy=default.target
        """;
    }

    private static string EscapeSystemdArgument(string argument)
    {
        string escapedArgument = argument
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("%", "%%");

        return $"\"{escapedArgument}\"";
    }

    private static async Task RunSystemctlAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool ignoreExitCode = false
    )
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
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
                "systemctl 실행 실패\n" +
                $"Exit code: {process.ExitCode}\n" +
                $"stdout: {standardOutput}\n" +
                $"stderr: {standardError}"
            );
        }
    }
}
