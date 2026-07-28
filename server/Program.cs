namespace DischeeseServer;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            await AutoStart.AutoStart.EnsureEnabledAsync();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"자동 실행 등록 실패: {exception}");
        }
    }
}
