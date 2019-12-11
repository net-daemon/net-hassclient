using System.Text.Json.Serialization;

namespace HassClient {

    public class MessageBase {
        [JsonPropertyName ("type")]
        public string Type { get; set; }
    }
    public class HassMessage : MessageBase {
        [JsonPropertyName ("message")]
        public string Message { get; set; }

        // [JsonPropertyName ("event")]
        // public string Event { get; set; }
    }
    public class AuthMessage : MessageBase {
        public AuthMessage () {
            this.Type = "auth";
        }

        [JsonPropertyName ("access_token")]
        public string AccessToken { get; set; }
    }

}