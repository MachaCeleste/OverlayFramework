using System.Net.WebSockets;

namespace OverlayFramework
{
    public class WebClient
    {
        public ClientType Client { get; set; }

        public WebSocket Socket { get; set; }

        public WebClient (ClientType client, WebSocket socket)
        {
            Client = client;
            Socket = socket;
        }
    }

    public enum ClientType
    {
        Chat,
        Notification,
        EmoteWall
    }
}
