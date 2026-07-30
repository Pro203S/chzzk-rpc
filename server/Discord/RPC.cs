using DiscordRPC;

namespace DischeeseServer.Discord
{
    public class RPC
    {
        public static readonly string CLIENT_ID = "1531667789180571779";

        public static readonly DiscordRpcClient rpc = new(CLIENT_ID);
        private static string? connectionError;

        public static string? ConnectionError =>
            Volatile.Read(ref connectionError);

        public static event Action<string?>? ConnectionErrorChanged;

        public static void Initialize()
        {
            rpc.OnReady += (sender, e) =>
            {
                Logger.Log($"[RPC] Connected to Discord (User: {e.User.DisplayName})");
                SetConnectionError(null);
            };

            rpc.OnPresenceUpdate += (sender, e) =>
            {
                Logger.Log("[RPC] Presence updated. (State = " + e.Presence.State + ")");
            };

            rpc.OnError += (sender, e) =>
            {
                Logger.Log("[RPC] Presence error. " + e.Code + " " + e.Message);
            };

            rpc.OnConnectionFailed += (sender, e) =>
            {
                string message =
                    "Discord RPC 연결에 실패했습니다. " +
                    $"(Pipe: {e.FailedPipe})";

                Logger.Log("[RPC] Connection failed. " + e.FailedPipe);
                SetConnectionError(message);
            };

            rpc.Initialize();

            AppDomain.CurrentDomain.ProcessExit += (sender, ev) =>
            {
                rpc.Dispose();
            };
        }

        private static void SetConnectionError(string? error)
        {
            Interlocked.Exchange(ref connectionError, error);
            ConnectionErrorChanged?.Invoke(error);
        }
    }
}
