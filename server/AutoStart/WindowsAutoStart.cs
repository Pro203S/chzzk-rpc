using Microsoft.Win32;

namespace DischeeseServer.AutoStart;

public sealed class WindowsAutoStart
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string applicationName;

    public WindowsAutoStart(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        this.applicationName = applicationName;
    }

    public Task EnsureEnabledAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        string commandLine = CreateCommandLine(
            ApplicationCommand
                .GetCurrent()
                .AppendArgument("--background")
        );

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(
            RunKeyPath,
            writable: true
        ) ?? throw new InvalidOperationException(
            "Windows 자동 실행 레지스트리를 열 수 없습니다."
        );

        string? registeredCommand = key.GetValue(applicationName) as string;

        if (!string.Equals(
            registeredCommand,
            commandLine,
            StringComparison.Ordinal
        ))
        {
            key.SetValue(
                applicationName,
                commandLine,
                RegistryValueKind.String
            );
        }

        return Task.CompletedTask;
    }

    public Task DisableAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
            RunKeyPath,
            writable: true
        );

        key?.DeleteValue(
            applicationName,
            throwOnMissingValue: false
        );

        return Task.CompletedTask;
    }

    private static string CreateCommandLine(
        ApplicationCommand command
    )
    {
        return string.Join(
            " ",
            new[] { command.FileName }
                .Concat(command.Arguments)
                .Select(QuoteArgument)
        );
    }

    private static string QuoteArgument(string argument)
    {
        return $"\"{argument.Replace("\"", "\\\"")}\"";
    }
}
