using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassEvent
    {
        public dynamic? Data { get; set; } // keeping set cause it is created after serialization

        [JsonPropertyName("data")]
        public JsonElement? DataElement { get; init; }

        [JsonPropertyName("event_type")]
        public string EventType { get; init; } = string.Empty;

        [JsonPropertyName("origin")]
        public string Origin { get; init; } = string.Empty;

        [JsonPropertyName("time_fired")]
        public DateTime? TimeFired { get; init; }
    }
}