using System.Text.Json.Serialization;

namespace OverlayFramework
{
    public class ChatMessage
    {
        [JsonPropertyName("user")]
        public string? User { get; set; }

        [JsonPropertyName("userColor")]
        public string? UserColor { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; } = 3000;
    }
}
