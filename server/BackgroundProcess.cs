using System.Diagnostics;

namespace DischeeseServer;

internal static class BackgroundProcess
{
    public const string Argument = "--background-process";

    public static void Start(IReadOnlyList<string> arguments)
    {
        Start(ApplicationCommand.GetCurrent(), arguments);
    }

    public static void Start(
        ApplicationCommand command,
        IReadOnlyList<string> arguments
    )
    {
        ProcessStartInfo startInfo = CreateStartInfo(command);

        foreach (string argument in arguments)
        {
            if (!string.Equals(
                argument,
                Argument,
                StringComparison.Ordinal
            ))
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        startInfo.ArgumentList.Add(Argument);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "백그라운드 서버 프로세스를 시작하지 못했습니다."
            );

        process.StandardInput.Close();
    }

    public static void DisconnectStandardStreams()
    {
        Console.SetIn(TextReader.Null);
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
    }

    private static ProcessStartInfo CreateStartInfo(
        ApplicationCommand command
    )
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = OperatingSystem.IsWindows()
                ? command.FileName
                : "/usr/bin/nohup",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add(command.FileName);
        }

        foreach (string argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
