using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassArea
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("area_id")]
        public string? Id { get; init; }
    }
}