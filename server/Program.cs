using DischeeseServer.Discord;
using DischeeseServer.WebSocket;

namespace DischeeseServer;

internal static class Program
{
    public static string Version = "v1.0.0";
    private static async Task Main(string[] args)
    {
        Logger.WriteToConsole = !args.Contains("--background");

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
