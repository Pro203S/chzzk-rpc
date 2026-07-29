using DiscordRPC;

namespace DischeeseServer.Discord
{
    public class RPC
    {
        public static readonly string CLIENT_ID = "1531667789180571779";

        public static readonly DiscordRpcClient rpc = new(CLIENT_ID);

        public static void Initialize()
        {
            rpc.OnReady += (sender, e) =>
            {
                Logger.Log($"[RPC] Connected to Discord (User: {e.User.DisplayName})");
            };

            rpc.OnPresenceUpdate += (sender, e) =>
            {
                Logger.Log("[RPC] Presence updated. (State = " + e.Presence.State + ")");
            };

            rpc.OnError += (sender, e) =>
            {
                Logger.Log("[RPC] Presence error. " + e.Code + " " + e.Message);
            };

            rpc.Initialize();

            AppDomain.CurrentDomain.ProcessExit += (sender, ev) =>
            {
                rpc.Dispose();
            };
        }
    }
}