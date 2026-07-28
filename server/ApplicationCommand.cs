using System.Reflection;

namespace DischeeseServer;

internal sealed record ApplicationCommand(
    string FileName,
    IReadOnlyList<string> Arguments
)
{
    public static ApplicationCommand GetCurrent()
    {
        string processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "현재 실행 파일 경로를 가져올 수 없습니다."
            );

        if (!IsDotNetHost(processPath))
        {
            return new ApplicationCommand(processPath, []);
        }

        string assemblyPath = Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException(
                "현재 애플리케이션 어셈블리 경로를 가져올 수 없습니다."
            );

        return new ApplicationCommand(
            processPath,
            [assemblyPath]
        );
    }

    private static bool IsDotNetHost(string processPath)
    {
        return string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase
        );
    }
}
