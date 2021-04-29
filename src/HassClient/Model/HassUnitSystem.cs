using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassUnitSystem
    {
        [JsonPropertyName("length")]
        public string? Length { get; set; }

        [JsonPropertyName("mass")]
        public string? Mass { get; set; }

        [JsonPropertyName("temperature")]
        public string? Temperature { get; set; }

        [JsonPropertyName("volume")]
        public string? Volume { get; set; }
    }
}