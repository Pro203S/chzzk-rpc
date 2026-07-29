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

        byte[] buffer = new byte[1024 * 4];

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
                    continue;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Invalid mesage type", CancellationToken.None);
                    Logger.Log("Disconnected " + context.Request.RemoteEndPoint + " (1003)");
                    continue;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                if (message == "ping")
                {
                    await webSocket.SendAsync(
                        Encoding.UTF8.GetBytes("pong"),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
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

                        await webSocket.SendAsync(
                            Encoding.UTF8.GetBytes("done"),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }

                    async void OnPresenceError(object sender, DiscordRPC.Message.ErrorMessage errorMessage)
                    {
                        Unsubscribe();

                        await webSocket.SendAsync(
                            Encoding.UTF8.GetBytes("error " + errorMessage.Message),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }

                    RPC.rpc.OnPresenceUpdate += OnPresenceUpdated;
                    RPC.rpc.OnError += OnPresenceError;

                    RPC.rpc.ClearPresence();
                    continue;
                }

                if (message.StartsWith("presence"))
                {
                    string payload = message.Split(" ")[1];
                    string[] data = payload.Split("\u0007");

                    if (data.Length < 4)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid payload data", CancellationToken.None);
                        Logger.Log("Disconnected " + context.Request.RemoteEndPoint + " (1007)");
                    }

                    void Unsubscribe()
                    {
                        RPC.rpc.OnPresenceUpdate -= OnPresenceUpdated;
                        RPC.rpc.OnError -= OnPresenceError;
                    }

                    async void OnPresenceUpdated(object sender, DiscordRPC.Message.PresenceMessage presenceMessage)
                    {
                        Unsubscribe();

                        await webSocket.SendAsync(
                            Encoding.UTF8.GetBytes("done"),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }

                    async void OnPresenceError(object sender, DiscordRPC.Message.ErrorMessage errorMessage)
                    {
                        Unsubscribe();

                        await webSocket.SendAsync(
                            Encoding.UTF8.GetBytes("error " + errorMessage.Message),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }

                    RPC.rpc.OnPresenceUpdate += OnPresenceUpdated;
                    RPC.rpc.OnError += OnPresenceError;

                    // data[0] = 스트리머
                    // data[1] = 방제
                    // data[2] = 방송 URL
                    // data[3] = 프로필 사진 URL

                    Logger.Log("Received presence: " + string.Join(", ", data));

                    RPC.rpc.SetPresence(new RichPresence()
                    {
                        Details = $"{data[0]}의 '{data[1]}' 보는 중",
                        State = data[0],
                        Type = ActivityType.Watching,
                        Buttons = [new Button() { Label = "방송 보기", Url = data[2] }],
                        Assets = new Assets()
                        {
                            LargeImageKey = data[3],
                            LargeImageUrl = data[2],
                            LargeImageText = data[0],
                            SmallImageText = "치지직",
                            SmallImageKey = "chzzk"
                        },
                    });

                    continue;
                }

                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid command", CancellationToken.None);
                Logger.Log("Disconnected " + context.Request.RemoteEndPoint + " (1007)");
                continue;
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
            webSocket.Dispose();
        }
    }
}
