using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    /// <summary>
    ///     Represents a target for a service call in Home Assistant
    /// </summary>
    public record HassTarget()
    {
        /// <summary>
        ///     Zero or more entity id to target with the service call
        /// </summary>
        [JsonPropertyName("entity_id")]
        public IEnumerable<string>? EntityIds { get; init; }

        /// <summary>
        ///     Zero or more device id to target with the service call
        /// </summary>
        [JsonPropertyName("device_id")]
        public IEnumerable<string>? DeviceIds { get; init; }

        /// <summary>
        ///     Zero or more area id to target with the service call
        /// </summary>
        [JsonPropertyName("area_id")]
        public IEnumerable<string>? AreaIds { get; init; }
    }
}