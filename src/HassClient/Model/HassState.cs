using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public class HassStates : List<HassState>
    {
    }
    
    public record HassState
    {
        [JsonPropertyName("attributes")] public Dictionary<string, object>? Attributes { get; set; }
        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";

        [JsonPropertyName("last_changed")] public DateTime LastChanged { get; set; } = DateTime.MinValue;
        [JsonPropertyName("last_updated")] public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        [JsonPropertyName("state")] public dynamic? State { get; set; } = "";
        [JsonPropertyName("context")] public HassContext? Context { get; set; }
    }
}