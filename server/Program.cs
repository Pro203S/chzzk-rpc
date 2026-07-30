using DischeeseServer.Discord;
using DischeeseServer.Utils;
using DischeeseServer.WebSocket;

namespace DischeeseServer;

internal static class Program
{
    public static string Version = "v1.0.0";
    private static async Task Main(string[] args)
    {
        bool openConsole = args.Contains("--open-console");
        bool isBackgroundProcess = args.Contains(
            BackgroundProcess.Argument
        );

        if (!openConsole && !isBackgroundProcess)
        {
            BackgroundProcess.Start(args);
            return;
        }

        if (isBackgroundProcess)
        {
            BackgroundProcess.DisconnectStandardStreams();
        }

        if (openConsole)
        {
            ProcessConsole.EnsureAvailable();
        }

        Logger.WriteToConsole = openConsole;

        try
        {
            Logger.Log("Starting Discheese server...");

            Logger.Log("Got arguments: " + string.Join(',', args));
            if (args.Contains("--register-autostart"))
            {
                Logger.Log("Registering AutoStart...");
                await AutoStart.AutoStart.EnsureEnabledAsync();
            }

            if (args.Contains("--unregister-autostart"))
            {
                Logger.Log("Unregistering AutoStart...");
                await AutoStart.AutoStart.DisableAsync();
            }

            Logger.Log("Initializing Discord RPC...");
            RPC.Initialize();

            int port = 58127;
            string? rawPortArg = Array.Find(args, v => v.StartsWith("--port="));

            if (!string.IsNullOrEmpty(rawPortArg))
            {
                port = Convert.ToInt32(rawPortArg.Split("=")[1]);
            }

            Server server = new(port);
            await server.Listen();

            Logger.Log("Listening on port " + port);

            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Logger.Log("An error occurred.");
            Logger.Log(ex.Message);
            Logger.Log(ex.StackTrace ?? "(Stack trace not available)");
        }
    }
}
