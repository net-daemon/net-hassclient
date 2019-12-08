using System.Text.Json.Serialization;

namespace HassClient {

    public class Message {
        [JsonPropertyName ("type")]
        public string Type { get; set; }
    }

}