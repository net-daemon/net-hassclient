using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Messages
{
    public record HassAuthMessage : HassMessageBase
    {
        public HassAuthMessage() => Type = "auth";

        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}