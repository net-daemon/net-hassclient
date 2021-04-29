using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public class HassAreas : List<HassArea>
    {
    }
    
    public record HassArea
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("area_id")]
        public string? Id { get; set; }
    }
}