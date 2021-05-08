using System.Text.Json;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassServiceEventData
    {
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonPropertyName("service")]
        public string Service { get; set; } = string.Empty;

        [JsonPropertyName("service_data")]
        public JsonElement? ServiceData { get; set; }

        public dynamic? Data { get; set; }
    }
}