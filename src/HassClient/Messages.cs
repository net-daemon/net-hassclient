using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HassClient {

    public interface IMessageHasId {
        int Id { get; set; }
    }
    public class MessageBase {

        [JsonPropertyName ("type")]
        public string Type { get; set; } = "";
    }
    public class HassMessage : MessageBase {
        [JsonPropertyName ("id")]
        public int Id { get; set; } = 0;

        [JsonPropertyName ("message")]
        public string? Message { get; set; }

        [JsonPropertyName ("success")]
        public bool? Success { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ? ExtensionData { get; set; }
        // [JsonPropertyName ("event")]
        // public string Event { get; set; }
    }
    public class AuthMessage : MessageBase {
        public AuthMessage () => this.Type = "auth";

        [JsonPropertyName ("access_token")]
        public string AccessToken { get; set; } = "";
    }

    public class SubscribeEventMessage : MessageBase, IMessageHasId {
        public SubscribeEventMessage () => this.Type = "subscribe_events";

        [JsonPropertyName ("id")]
        public int Id { get; set; } = 0;

        [JsonPropertyName ("event_type")]
        public string? EventType { get; set; }
    }

}