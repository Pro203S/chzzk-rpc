using System.Net;
using System.Net.WebSockets;
using Socket = System.Net.WebSockets.WebSocket;
using System.Text;
using Timer = System.Timers.Timer;
using DischeeseServer.Discord;
using DiscordRPC;
using DiscordRPC.Events;

namespace DischeeseServer.WebSocket;

public class Server
{
    public int PORT { get; }

    private readonly HttpListener listener = new();

    public Server(int port)
    {
        PORT = port;
        listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public async Task Listen()
    {
        listener.Start();

        Timer timer = new(1);
        timer.Elapsed += async (sender, ev) =>
        {
            HttpListenerContext context = await listener.GetContextAsync();

            Logger.Log(context.Request.RemoteEndPoint + " Connected");

            if (context.Request.IsWebSocketRequest)
            {
                Logger.Log("Upgrading to WebSocket connection...");
                _ = Task.Run(() => HandleConnection(context));
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                Logger.Log("Rejected non-WebSocket request.");
            }
        };
        timer.AutoReset = true;
        timer.Enabled = true;
    }

    public async Task HandleConnection(HttpListenerContext context)
    {
        HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
        Socket webSocket = wsContext.WebSocket;
        SemaphoreSlim sendLock = new(1, 1);
        int protocolReady = 0;

        byte[] buffer = new byte[1024 * 4];

        async Task SendTextAsync(string message)
        {
            await sendLock.WaitAsync();

            try
            {
                if (webSocket.State != WebSocketState.Open)
                {
                    return;
                }

                await webSocket.SendAsync(
                    Encoding.UTF8.GetBytes(message),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
            catch (WebSocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                sendLock.Release();
            }
        }

        async Task SendRpcConnectionStateAsync()
        {
            if (Volatile.Read(ref protocolReady) == 0)
            {
                return;
            }

            string message = RPC.ConnectionError is string error
                ? $"error {error}"
                : "error-clear";

            await SendTextAsync(message);
        }

        async void OnRpcConnectionErrorChanged(string? _)
        {
            await SendRpcConnectionStateAsync();
        }

        RPC.ConnectionErrorChanged += OnRpcConnectionErrorChanged;

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    Logger.Log("Disconnected " + context.Request.RemoteEndPoint + " (1000)");
                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await webSocket.CloseOutputAsync(WebSocketCloseStatus.InvalidMessageType, "Invalid message type", CancellationToken.None);
                    Logger.Log("Disconnected " + context.Request.RemoteEndPoint + " (1003)");
                    return;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                if (message == "ping")
                {
                    await SendTextAsync("pong");
                    continue;
                }

                if (message == "clear")
                {
                    void Unsubscribe()
                    {
                        RPC.rpc.OnPresenceUpdate -= OnPresenceUpdated;
                        RPC.rpc.OnError -= OnPresenceError;
                    }

                    async void OnPresenceUpdated(object sender, DiscordRPC.Message.PresenceMessage presenceMessage)
                    {
                        Unsubscribe();

                        await SendTextAsync("done");
                    }

                    async void OnPresenceError(object sender, DiscordRPC.Message.ErrorMessage errorMessage)
                    {
                        Unsubscribe();

                        await SendTextAsync(
                            "error " + errorMessage.Message
                        );
                    }

                    RPC.rpc.OnPresenceUpdate += OnPresenceUpdated;
                    RPC.rpc.OnError += OnPresenceError;

                    RPC.rpc.ClearPresence();
                    continue;
                }

                if (message.StartsWith("presence "))
                {
                    string payload = message["presence ".Length..];
                    string[] data = payload.Split("\u0007");
                    StatusDisplayType statusDisplay = StatusDisplayType.Name;

                    if (
                        data.Length is < 4 or > 6 ||
                        (data.Length >= 5 && !bool.TryParse(data[4], out _)) ||
                        (
                            data.Length == 6 &&
                            (
                                !Enum.TryParse(data[5], true, out statusDisplay) ||
                                !Enum.IsDefined(statusDisplay)
                            )
                        )
                    )
                    {
                        await webSocket.CloseOutputAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid payload data", CancellationToken.None);
                        Logger.Log("Disconnected " + context.Request.RemoteEndPoint + " (1007)");
                        return;
                    }

                    bool showSmallImage =
                        data.Length >= 5 && bool.Parse(data[4]);

                    void Unsubscribe()
                    {
                        RPC.rpc.OnPresenceUpdate -= OnPresenceUpdated;
                        RPC.rpc.OnError -= OnPresenceError;
                    }

                    async void OnPresenceUpdated(object sender, DiscordRPC.Message.PresenceMessage presenceMessage)
                    {
                        Unsubscribe();

                        await SendTextAsync("done");
                    }

                    async void OnPresenceError(object sender, DiscordRPC.Message.ErrorMessage errorMessage)
                    {
                        Unsubscribe();

                        await SendTextAsync(
                            "error " + errorMessage.Message
                        );
                    }

                    RPC.rpc.OnPresenceUpdate += OnPresenceUpdated;
                    RPC.rpc.OnError += OnPresenceError;

                    // data[0] = 스트리머
                    // data[1] = Details
                    // data[2] = 방송 URL
                    // data[3] = 프로필 사진 URL
                    // data[4] = Small image 설정 여부 (false, true)
                    // data[5] = Status Display

                    Logger.Log("Received presence: " + string.Join(", ", data));

                    Assets assets = new()
                    {
                        LargeImageKey = data[3],
                        LargeImageUrl = data[2],
                        LargeImageText = data[0],
                    };

                    if (showSmallImage)
                    {
                        assets.SmallImageText = "치지직";
                        assets.SmallImageKey = "chzzk";
                    }

                    RPC.rpc.SetPresence(new RichPresence()
                    {
                        Details = data[1],
                        State = data[0],
                        Type = ActivityType.Watching,
                        StatusDisplay = statusDisplay,
                        Buttons = [new Button() { Label = "방송 보기", Url = data[2] }],
                        Assets = assets,
                    });

                    continue;
                }

                if (message == "user")
                {
                    var currentUser = RPC.rpc.CurrentUser;
                    if (currentUser == null)
                    {
                        await SendTextAsync("null");
                        continue;
                    }

                    await SendTextAsync(
                        $"{currentUser.Username}\u0007" +
                        $"{currentUser.DisplayName}\u0007" +
                        currentUser.GetAvatarURL(
                            currentUser.IsAvatarAnimated
                                ? User.AvatarFormat.GIF
                                : User.AvatarFormat.PNG
                        )
                    );
                    continue;
                }

                if (message == "version")
                {
                    await SendTextAsync(Program.Version);
                    Volatile.Write(ref protocolReady, 1);
                    await SendRpcConnectionStateAsync();
                    continue;
                }

                await webSocket.CloseOutputAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid command", CancellationToken.None);
                Logger.Log("Disconnected " + context.Request.RemoteEndPoint + " (1007)");
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.Log("An error occurred.");
            Logger.Log(ex.Message);
            Logger.Log(ex.StackTrace ?? "(Stack trace not available)");
        }
        finally
        {
            RPC.ConnectionErrorChanged -= OnRpcConnectionErrorChanged;
            webSocket.Dispose();
        }
    }
}
