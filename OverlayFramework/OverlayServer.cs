using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace OverlayFramework
{
    public class OverlayServer : IDisposable
    {
        public static OverlayServer Singleton { get; private set; }

        private HttpListener _listener;
        private readonly List<WebSocket> _clients;
        private readonly object _clientLock = new();
        private readonly CancellationTokenSource _cts;

        private int _listenPort;
        private string _htmlPath = "overlay.html";

        public OverlayServer(int listenPort = 23399)
        {
            _listenPort = listenPort;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_listenPort}/");
            _clients = new List<WebSocket>();
            _cts = new CancellationTokenSource();
            OverlayServer.Singleton = this;
        }

        public void Start()
        {
            _listener.Start();
            Task.Run(StartListening);
        }

        public int GetNumClients()
        {
            lock (_clientLock) return _clients.Count;
        }

        public async Task SendMessage(string user, string message, string userColor = "#000000", int duration = 3000)
        {
            NetMessage msg = new NetMessage(MessageType.Message);
            msg.AddString(user);
            msg.AddString(message);
            msg.AddString(userColor);
            msg.AddInt(duration);
            await SendNetMessage(msg);
        }

        public async Task SendNotification(string user, string title, string message, int duration = 3000)
        {
            NetMessage msg = new NetMessage(MessageType.Notification);
            msg.AddString(user);
            msg.AddString(title);
            msg.AddString(message);
            msg.AddInt(duration);
            await SendNetMessage(msg);
        }

        private async Task SendNetMessage(NetMessage msg)
        {
            var json = JsonSerializer.Serialize(msg);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            List<WebSocket> @lock;
            lock (_clientLock) @lock = [.. _clients];
            foreach (var client in @lock)
            {
                if (client.State != WebSocketState.Open) continue;
                await client.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
            }
        }

        private async Task StartListening()
        {
            while (!_cts.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();
                var path = context.Request.Url!.AbsolutePath;

                if (path == "/ws" || path == "/ws/")
                {
                    if (!context.Request.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        continue;
                    }
                    _ = HandleWebSocket(context);
                }
                else
                    ServeHtml(context);
            }
        }

        private void ServeHtml(HttpListenerContext context)
        {
            if (!File.Exists(_htmlPath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            byte[] bytes = File.ReadAllBytes(_htmlPath);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes);
            context.Response.Close();
        }

        private async Task HandleWebSocket(HttpListenerContext context)
        {
            var wsContent = await context.AcceptWebSocketAsync(null);
            var socket = wsContent.WebSocket;
            lock (_clientLock) _clients.Add(socket);

            var buffer = new byte[10000];

            try
            {
                while (socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var res = await socket.ReceiveAsync(buffer, _cts.Token);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                }
            }
            finally
            {
                lock (_clientLock) _clients.Remove(socket);
                socket.Dispose();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();

            foreach (var client in _clients)
            {
                client.Dispose();
            }
        }
    }
}
