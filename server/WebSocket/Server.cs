using System.Net;
using System.Net.WebSockets;
using Socket = System.Net.WebSockets.WebSocket;
using System.Text;
using Timer = System.Timers.Timer;

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

            Logger.Log("Request from " + context.Request.RemoteEndPoint);

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

    static async Task HandleConnection(HttpListenerContext context)
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
                Console.WriteLine($"Received: {message}");

                // Echo back the message to the client
                byte[] responseBuffer = Encoding.UTF8.GetBytes($"Echo: {message}");
                await webSocket.SendAsync(
                    new ArraySegment<byte>(responseBuffer),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None);
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