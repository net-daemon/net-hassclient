using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassError
    {
        [JsonPropertyName("code")]
        public object? Code { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}