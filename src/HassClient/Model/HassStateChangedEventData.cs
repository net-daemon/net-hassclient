using JoySoftware.HomeAssistant.Helpers.Json;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassStateChangedEventData
    {
        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; } = "";

        [JsonConverter(typeof(HassStateConverter))]
        [JsonPropertyName("new_state")]
        public HassState? NewState { get; set; }

        [JsonConverter(typeof(HassStateConverter))]
        [JsonPropertyName("old_state")]
        public HassState? OldState { get; set; }
    }
}