namespace DischeeseServer.AutoStart;

internal static class AutoStart
{
    public static Task EnsureEnabledAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsAutoStart autoStart = new("Discheese");
            return autoStart.EnsureEnabledAsync(cancellationToken);
        }

        if (OperatingSystem.IsLinux())
        {
            LinuxAutoStart autoStart = new("discheese.service");
            return autoStart.EnsureEnabledAsync(cancellationToken);
        }

        if (OperatingSystem.IsMacOS())
        {
            MacOsAutoStart autoStart = new(
                applicationId: "kr.pro203s.discheese"
            );

            return autoStart.EnsureEnabledAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }
}
