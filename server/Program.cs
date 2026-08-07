using DischeeseServer.Discord;
using DischeeseServer.Update;
using DischeeseServer.Utils;
using DischeeseServer.WebSocket;

namespace DischeeseServer;

internal static class Program
{
    private static readonly CancellationTokenSource Shutdown = new();

    public static string ProtocolVersion = "v1.1.0";

    public static void RequestShutdown()
    {
        Shutdown.Cancel();
    }

    private static async Task Main(string[] args)
    {
        if (ServerUpdater.IsApplyUpdate(args))
        {
            try
            {
                await ServerUpdater.ApplyUpdateAsync(args);
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to apply the server update.");
                Logger.Log(ex.Message);
                Logger.Log(ex.StackTrace ?? "(Stack trace not available)");
            }

            return;
        }

        await ServerUpdater.CleanupPreviousUpdateAsync(args);

        bool openConsole = args.Contains("--open-console");
        bool isBackgroundProcess = args.Contains(
            BackgroundProcess.Argument
        );
        bool registerAutoStart = args.Contains("--register-autostart");
        bool unregisterAutoStart = args.Contains("--unregister-autostart");

        if (!openConsole && !isBackgroundProcess)
        {
            BackgroundProcess.Start(args);
            return;
        }

        bool processConfigured = false;

        void ConfigureProcess()
        {
            if (processConfigured)
            {
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
            processConfigured = true;
        }

        if (registerAutoStart || unregisterAutoStart)
        {
            ConfigureProcess();
        }

        try
        {
            if (registerAutoStart)
            {
                Logger.Log("Registering AutoStart...");
                await AutoStart.AutoStart.EnsureEnabledAsync();
            }

            if (unregisterAutoStart)
            {
                Logger.Log("Unregistering AutoStart...");
                await AutoStart.AutoStart.DisableAsync();
            }

            using SingleInstanceLock? singleInstance =
                SingleInstanceLock.TryAcquire();

            if (singleInstance is null)
            {
                return;
            }

            ConfigureProcess();

            Logger.Log("Starting Discheese server...");
            Logger.Log("Got arguments: " + string.Join(',', args));

            Logger.Log("Initializing Discord RPC...");
            RPC.Initialize();

            int port = 58127;
            string? rawPortArg = Array.Find(args, v => v.StartsWith("--port="));

            if (!string.IsNullOrEmpty(rawPortArg))
            {
                port = Convert.ToInt32(rawPortArg.Split("=")[1]);
            }

            Server server = new(port);
            await server.Listen(Shutdown.Token);
        }
        catch (Exception ex)
        {
            Logger.Log("An error occurred.");
            Logger.Log(ex.Message);
            Logger.Log(ex.StackTrace ?? "(Stack trace not available)");
        }
    }
}
