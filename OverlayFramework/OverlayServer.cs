using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace OverlayFramework
{
    public class OverlayServer : IDisposable
    {
        public static OverlayServer Singleton { get; private set; }

        public int MessageDuration = 5000;
        public int NotificationDuration = 9000;

        private static string _appdata;

        private HttpListener _listener;
        private readonly List<WebClient> _clients;
        private readonly object _clientLock = new();
        private readonly CancellationTokenSource _cts;

        private int _listenPort;

        public OverlayServer(int listenPort = 23399)
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            _appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                fileInfo.CompanyName,
                "temp",
                fileInfo.ProductName
            );
            if (!Directory.Exists(_appdata)) Directory.CreateDirectory(_appdata);
            UnpackResources();
            _listenPort = listenPort;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_listenPort}/");
            _clients = new();
            _cts = new CancellationTokenSource();
            _listener.Start();
            Task.Run(StartListening);
            OverlayServer.Singleton = this;
        }

        /// <summary>
        /// Get the number of clients listening to chat
        /// </summary>
        /// <returns></returns>
        public int GetNumChatClients()
        {
            return _clients.Where(x => x.Client == ClientType.Chat).Count();
        }

        /// <summary>
        /// Get the number of clients listening to notifications
        /// </summary>
        /// <returns></returns>
        public int GetNumNotifClients()
        {
            return _clients.Where(x => x.Client == ClientType.Notification).Count();
        }

        /// <summary>
        /// Send a chat message to all clients listening to chat
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        /// <param name="userColor"></param>
        /// <returns></returns>
        public async Task SendMessage(string user, string message, string userColor = "#a970ff")
        {
            ChatMessage msg = new ChatMessage();
            msg.User = $"<span class=\"user\" style=\"color: {userColor}\">{user}</span>";
            msg.Content = message;
            msg.UserColor = userColor;
            msg.Duration = MessageDuration;
            var json = JsonSerializer.Serialize(msg);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await SendClientMessage(ClientType.Chat, bytes);
        }

        /// <summary>
        /// Send a notification to all clients listening to notifications
        /// </summary>
        /// <param name="user"></param>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="userColor"></param>
        /// <returns></returns>
        public async Task SendNotification(string user, string title, string message, string userColor = "#a970ff")
        {
            Notification notif = new Notification();
            notif.Title = $"<span class=\"user\" style=\"color: {userColor}\">{user}</span> {title}";
            notif.Content = message;
            notif.Duration = NotificationDuration;
            var json = JsonSerializer.Serialize(notif);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await SendClientMessage(ClientType.Notification, bytes);
        }

        private async Task SendClientMessage(ClientType type, byte[] data)
        {
            List<WebClient> @lock;
            lock (_clientLock) @lock = [.. _clients];
            foreach (var client in @lock.Where(x => x.Client == type))
            {
                if (client.Socket.State != WebSocketState.Open) continue;
                await client.Socket.SendAsync(data, WebSocketMessageType.Text, true, _cts.Token);
            }
        }

        private void UnpackResources()
        {
            var assembly = typeof(OverlayServer).Assembly;
            var resources = this.GetType().Assembly.GetManifestResourceNames();
            foreach (var resource in resources)
            {
                if (!resource.Contains(".Overlays.")) continue;
                using Stream? stream = assembly.GetManifestResourceStream(resource);
                if (stream == null) continue;
                string[] parts = resource.Split('.');
                string fileName = parts[^2] + "." + parts[^1];
                using FileStream fileStream = new FileStream(Path.Combine(_appdata, fileName), FileMode.Create, FileAccess.Write);
                stream.CopyTo(fileStream);
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
                    ServeHtml(context, path);
            }
        }

        private void ServeHtml(HttpListenerContext context, string path)
        {
            string fileName = path.TrimStart('/');
            if (string.IsNullOrEmpty(fileName)) fileName = "index.html";
            fileName = Path.GetFileName(fileName);
            string filePath = Path.Combine(_appdata, fileName);
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                string extension = Path.GetExtension(filePath).ToLower();
                context.Response.ContentType = extension switch
                {
                    ".html" => "text/html",
                    ".css"  => "text/css",
                    ".js"   => "application/javascript",
                    _       => "application/octet-stream"
                };
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes);
            }
            catch
            {
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }

        private async Task HandleWebSocket(HttpListenerContext context)
        {
            if (!Enum.TryParse(context.Request.QueryString["type"], true, out ClientType clientType))
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }
            var wsContent = await context.AcceptWebSocketAsync(null);
            var socket = wsContent.WebSocket;
            var client = new WebClient(clientType, socket);
            lock (_clientLock) _clients.Add(client);
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
                lock (_clientLock) _clients.Remove(client);
                socket.Dispose();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            foreach (var client in _clients)
            {
                client.Socket.Dispose();
            }
        }
    }
}
