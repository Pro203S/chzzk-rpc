namespace DischeeseServer;

public class Logger
{
    public static bool WriteToConsole { get; set; } = true;

    public static string DirectoryPath { get; } = CreateDirectoryPath();

    public static string LogPath { get; } = CreateLogPath();

    private static string CreateDirectoryPath()
    {
        string directoryPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            ),
            "DischeeseServer"
        );

        Directory.CreateDirectory(directoryPath);

        return directoryPath;
    }

    private static string CreateLogPath()
    {
        return Path.Combine(
            DirectoryPath,
            $"discheese-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log"
        );
    }

    public static void Log(string str)
    {
        string logContent = $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] {str}{Environment.NewLine}";

        if (WriteToConsole)
        {
            Console.Write(logContent);
        }

        File.AppendAllText(LogPath, logContent);
    }
}
