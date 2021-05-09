using JoySoftware.HomeAssistant.Helpers.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public class HassStates : List<HassState>
    {
    }
    
    public record HassState
    {
        [JsonPropertyName("attributes")] public JsonElement? AttributesJson { get; set; }

        public IReadOnlyDictionary<string, object>? Attributes
        {
            get => AttributesJson?.ToObject<Dictionary<string, object>>() ?? new();
            init => AttributesJson = value.ToJsonElement();
        }

        public T? AttributesAs<T>() => AttributesJson.HasValue ? AttributesJson.Value.ToObject<T>() : default;

        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";

        [JsonPropertyName("last_changed")] public DateTime LastChanged { get; set; } = DateTime.MinValue;
        [JsonPropertyName("last_updated")] public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        [JsonPropertyName("state")] public dynamic? State { get; set; } = "";
        [JsonPropertyName("context")] public HassContext? Context { get; set; }
    }
}