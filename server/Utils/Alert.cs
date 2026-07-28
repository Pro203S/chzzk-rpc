using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DischeeseServer.Utils;

public static class Alert
{
    private const uint MessageBoxOk = 0x00000000;
    private const uint MessageBoxYesNo = 0x00000004;
    private const uint MessageBoxIconQuestion = 0x00000020;
    private const uint MessageBoxIconInformation = 0x00000040;
    private const uint MessageBoxSetForeground = 0x00010000;
    private const int MessageBoxResultYes = 6;

    public static bool confirm(
        string message,
        string title = "Discheese"
    )
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);

        if (OperatingSystem.IsWindows())
        {
            return ShowWindowsMessageBox(
                message,
                title,
                MessageBoxYesNo |
                    MessageBoxIconQuestion |
                    MessageBoxSetForeground
            ) == MessageBoxResultYes;
        }

        if (OperatingSystem.IsMacOS())
        {
            return RunProcess(
                "/usr/bin/osascript",
                [
                    "-e",
                    CreateMacOsConfirmScript(message, title)
                ],
                out int exitCode
            ) && exitCode == 0;
        }

        if (OperatingSystem.IsLinux())
        {
            if (RunProcess(
                "zenity",
                [
                    "--question",
                    $"--title={title}",
                    $"--text={message}",
                    "--ok-label=확인",
                    "--cancel-label=취소",
                    "--no-wrap"
                ],
                out int zenityExitCode
            ))
            {
                return zenityExitCode == 0;
            }

            if (RunProcess(
                "kdialog",
                [
                    "--yesno",
                    message,
                    "--title",
                    title
                ],
                out int kdialogExitCode
            ))
            {
                return kdialogExitCode == 0;
            }
        }

        return ConfirmInConsole(message, title);
    }

    public static void alert(
        string message,
        string title = "Discheese"
    )
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);

        if (OperatingSystem.IsWindows())
        {
            ShowWindowsMessageBox(
                message,
                title,
                MessageBoxOk |
                    MessageBoxIconInformation |
                    MessageBoxSetForeground
            );

            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            if (RunProcess(
                "/usr/bin/osascript",
                [
                    "-e",
                    CreateMacOsAlertScript(message, title)
                ],
                out int exitCode
            ) && exitCode == 0)
            {
                return;
            }
        }

        if (OperatingSystem.IsLinux())
        {
            if (RunProcess(
                "zenity",
                [
                    "--info",
                    $"--title={title}",
                    $"--text={message}",
                    "--ok-label=확인",
                    "--no-wrap"
                ],
                out int zenityExitCode
            ) && zenityExitCode == 0)
            {
                return;
            }

            if (RunProcess(
                "kdialog",
                [
                    "--msgbox",
                    message,
                    "--title",
                    title
                ],
                out int kdialogExitCode
            ) && kdialogExitCode == 0)
            {
                return;
            }
        }

        Console.WriteLine($"[{title}] {message}");
    }

    private static bool ConfirmInConsole(
        string message,
        string title
    )
    {
        if (Console.IsInputRedirected)
        {
            return false;
        }

        Console.Write($"[{title}] {message} [y/N]: ");

        string? answer = Console.ReadLine();

        return string.Equals(
            answer,
            "y",
            StringComparison.OrdinalIgnoreCase
        ) || string.Equals(
            answer,
            "yes",
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static string CreateMacOsConfirmScript(
        string message,
        string title
    )
    {
        return
            $"display dialog \"{EscapeAppleScript(message)}\" " +
            $"with title \"{EscapeAppleScript(title)}\" " +
            "buttons {\"취소\", \"확인\"} " +
            "default button \"확인\" cancel button \"취소\"";
    }

    private static string CreateMacOsAlertScript(
        string message,
        string title
    )
    {
        return
            $"display dialog \"{EscapeAppleScript(message)}\" " +
            $"with title \"{EscapeAppleScript(title)}\" " +
            "buttons {\"확인\"} default button \"확인\"";
    }

    private static string EscapeAppleScript(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static bool RunProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        out int exitCode
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

        try
        {
            process.Start();
            process.WaitForExit();

            exitCode = process.ExitCode;
            return true;
        }
        catch (Win32Exception)
        {
            exitCode = -1;
            return false;
        }
    }

#pragma warning disable SYSLIB1054
    [DllImport(
        "user32.dll",
        EntryPoint = "MessageBoxW",
        CharSet = CharSet.Unicode
    )]
    private static extern int ShowWindowsMessageBox(
        nint windowHandle,
        string message,
        string title,
        uint type
    );
#pragma warning restore SYSLIB1054

    private static int ShowWindowsMessageBox(
        string message,
        string title,
        uint type
    )
    {
        return ShowWindowsMessageBox(
            nint.Zero,
            message,
            title,
            type
        );
    }
}
