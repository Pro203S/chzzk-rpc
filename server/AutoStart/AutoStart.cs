namespace DischeeseServer.AutoStart;

internal static class AutoStart
{
    public static Task EnsureEnabledAsync(
        CancellationToken cancellationToken = default
    )
    {
        return EnsureEnabledCoreAsync(cancellationToken);
    }

    private static async Task EnsureEnabledCoreAsync(
        CancellationToken cancellationToken
    )
    {
        ApplicationCommand installedCommand =
            await ApplicationInstaller.ReplaceCurrentAsync(
                cancellationToken
            );

        ApplicationCommand backgroundCommand = installedCommand
            .AppendArgument(BackgroundProcess.Argument);

        if (OperatingSystem.IsWindows())
        {
            WindowsAutoStart autoStart = new("Discheese");
            await autoStart.EnsureEnabledAsync(
                backgroundCommand,
                cancellationToken
            );
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            LinuxAutoStart autoStart = new("discheese.service");
            await autoStart.EnsureEnabledAsync(
                backgroundCommand,
                cancellationToken
            );
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            MacOsAutoStart autoStart = new(
                applicationId: "kr.pro203s.discheese"
            );

            await autoStart.EnsureEnabledAsync(
                backgroundCommand,
                cancellationToken
            );
        }
    }

    public static Task DisableAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsAutoStart autoStart = new("Discheese");
            return autoStart.DisableAsync(cancellationToken);
        }

        if (OperatingSystem.IsLinux())
        {
            LinuxAutoStart autoStart = new("discheese.service");
            return autoStart.DisableAsync(cancellationToken);
        }

        if (OperatingSystem.IsMacOS())
        {
            MacOsAutoStart autoStart = new(
                applicationId: "kr.pro203s.discheese"
            );

            return autoStart.DisableAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }
}
