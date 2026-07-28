namespace DischeeseServer;

internal static class Program
{
    private static async Task Main()
    {
        using CancellationTokenSource cancellationTokenSource = new();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            cancellationTokenSource.Cancel();
        };

        try
        {
            await AutoStart.EnsureEnabledAsync(
                cancellationTokenSource.Token
            );
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"자동 실행 등록 실패: {exception}"
            );
        }

        try
        {
            await RunAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // 정상 종료
        }
    }

    private static async Task RunAsync(
        CancellationToken cancellationToken
    )
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await PerformBackgroundWorkAsync(cancellationToken);

            await Task.Delay(
                TimeSpan.FromMinutes(1),
                cancellationToken
            );
        }
    }

    private static async Task PerformBackgroundWorkAsync(
        CancellationToken cancellationToken
    )
    {
        string dataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            ),
            "DischeeseServer"
        );

        Directory.CreateDirectory(dataDirectory);

        string logPath = Path.Combine(
            dataDirectory,
            string.Format("discheese-{0}.log", DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))
        );

        await File.AppendAllTextAsync(
            logPath,
            $"[{DateTimeOffset.Now}] Background task executed{Environment.NewLine}",
            cancellationToken
        );
    }
}
