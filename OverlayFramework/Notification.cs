using System.Text.Json.Serialization;

namespace OverlayFramework
{
    public class Notification
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; } = 3000;
    }
}
