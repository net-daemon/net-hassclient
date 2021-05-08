using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Messages
{
    public record CallServiceCommand : CommandMessage
    {
        public CallServiceCommand() => Type = "call_service";

        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonPropertyName("service")]
        public string Service { get; set; } = string.Empty;

        [JsonPropertyName("service_data")]
        public object? ServiceData { get; set; }
    }
}