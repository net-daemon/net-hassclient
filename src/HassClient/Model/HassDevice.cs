using JoySoftware.HomeAssistant.Helpers.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public class HassDevices : List<HassDevice>
    {
    }
    
    public record HassDevice
    {
        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }

        [JsonPropertyName("model")]
        [JsonConverter(typeof(HassDeviceModelConverter))]
        public string? Model { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("area_id")]
        public string? AreaId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_by_user")]
        public string? NameByUser { get; set; }
    }
}