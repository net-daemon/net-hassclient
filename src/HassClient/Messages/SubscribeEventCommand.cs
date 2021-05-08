using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Messages
{
    public record SubscribeEventCommand : CommandMessage
    {
        public SubscribeEventCommand() => Type = "subscribe_events";

        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }
    }
}