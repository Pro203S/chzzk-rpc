using System.Reflection;

namespace DischeeseServer;

internal sealed record ApplicationCommand(
    string FileName,
    IReadOnlyList<string> Arguments
)
{
    public string ApplicationPath =>
        IsDotNetHost(FileName)
            ? Arguments.FirstOrDefault() ?? FileName
            : FileName;

    public ApplicationCommand AppendArgument(string argument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument);

        return this with
        {
            Arguments = Arguments
                .Append(argument)
                .ToArray()
        };
    }

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

        string assemblyName = Assembly.GetEntryAssembly()?.GetName().Name
            ?? throw new InvalidOperationException(
                "현재 애플리케이션 어셈블리 이름을 가져올 수 없습니다."
            );

        string assemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{assemblyName}.dll"
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
