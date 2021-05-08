using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Messages
{
    public record HassMessageBase
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }
}