using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public class HassEntities : List<HassEntity>
    {
    }

    public record HassEntity
    {
        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("entity_id")]
        public string? EntityId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }
    }
}