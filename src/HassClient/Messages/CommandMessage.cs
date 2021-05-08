using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Messages
{
    public record CommandMessage : HassMessageBase
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }
}