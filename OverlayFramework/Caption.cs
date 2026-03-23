using System.Text.Json.Serialization;

namespace OverlayFramework
{
    public class Caption
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        [JsonPropertyName("final")]
        public bool Final { get; set; }
    }
}
