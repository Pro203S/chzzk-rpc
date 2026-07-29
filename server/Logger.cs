using System.Diagnostics;

namespace DischeeseServer;

public class Logger
{
    public static string LogPath
    {
        get
        {
            string path = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                ),
                "DischeeseServer"
            );
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }
    }

    public static void Log(string str)
    {
        string logPath = Path.Combine(
            LogPath,
            string.Format("discheese-{0}.log", DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))
        );

        string logContent = $"[{DateTimeOffset.Now}] {str}{Environment.NewLine}";

        Console.Write(logContent);
        File.AppendAllText(logPath, logContent);
    }
}