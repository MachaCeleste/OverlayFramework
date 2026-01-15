using System.Text.Json.Serialization;

namespace OverlayFramework
{
    public class Emote
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
