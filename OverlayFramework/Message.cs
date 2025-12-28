using System.Text.Json.Serialization;

namespace OverlayFramework
{
    public class Message
    {
        [JsonInclude]
        [JsonPropertyName("stringValues")]
        protected List<string> StringValues = new List<string>();

        [JsonInclude]
        [JsonPropertyName("intValues")]
        protected List<int> IntValues = new List<int>();

        [JsonInclude]
        [JsonPropertyName("boolValues")]
        protected List<bool> BoolValues = new List<bool>();

        [JsonConstructor]
        public Message() { }

        public void AddString(string value)
        {
            this.StringValues.Add(value);
        }

        public void AddInt(int value)
        {
            this.IntValues.Add(value);
        }

        public void AddBool(bool value)
        {
            this.BoolValues.Add(value);
        }
    }
}
