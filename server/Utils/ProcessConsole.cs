using System.Runtime.InteropServices;

namespace DischeeseServer.Utils;

internal static class ProcessConsole
{
    private const uint AttachParentProcess = uint.MaxValue;
    private const int ErrorAccessDenied = 5;

    public static void EnsureAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        bool consoleAvailable = AttachConsole(AttachParentProcess);

        if (!consoleAvailable)
        {
            int error = Marshal.GetLastWin32Error();

            consoleAvailable =
                error == ErrorAccessDenied ||
                AllocConsole();
        }

        if (!consoleAvailable)
        {
            return;
        }

        Console.SetIn(new StreamReader(Console.OpenStandardInput()));
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput())
        {
            AutoFlush = true
        });
        Console.SetError(new StreamWriter(Console.OpenStandardError())
        {
            AutoFlush = true
        });
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();
#pragma warning restore SYSLIB1054
}
