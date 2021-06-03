using JoySoftware.HomeAssistant.Helpers.Json;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassStateChangedEventData
    {
        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; } = "";

        [JsonPropertyName("new_state")]
        public HassState? NewState { get; set; }

        [JsonPropertyName("old_state")]
        public HassState? OldState { get; set; }
    }
}