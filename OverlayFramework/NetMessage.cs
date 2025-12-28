using System.Text.Json.Serialization;

namespace OverlayFramework
{
    public class NetMessage : Message
    {
        public MessageType Type { get; set; }

        [JsonConstructor]
        private NetMessage() { }

        public NetMessage(MessageType ID)
        {
            this.Type = ID;
        }
    }

    public enum MessageType
    {
        Message,
        Notification
    }
}
