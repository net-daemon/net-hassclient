using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Client
{
    public class HassMessageBase
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
    }

    #region -- Receiving messages --

    public class HassMessage : HassMessageBase
    {
        [JsonPropertyName("id")] public int Id { get; set; } = 0;

        [JsonPropertyName("message")] public string? Message { get; set; }

        [JsonPropertyName("success")] public bool? Success { get; set; }

        [JsonPropertyName("event")]
        [JsonConverter(typeof(HassEventConverter))]
        public HassEvent? Event { get; set; }

        [JsonPropertyName("result")] public JsonElement? ResultElement { get; set; } = null;

        public object? Result { get; set; }
    }

    public class HassEvent
    {
        [JsonPropertyName("event_type")] public string EventType { get; set; } = "";

        [JsonPropertyName("origin")] public string Origin { get; set; } = "";

        [JsonPropertyName("time_fired")] public DateTime? TimeFired { get; set; } = null;

        [JsonPropertyName("data")] public JsonElement? DataElement { get; set; } = null;

        public HassEventData? Data { get; set; }
    }

    public class HassEventData
    {
    }

    public class HassServiceEventData : HassEventData
    {
        [JsonPropertyName("domain")] public string Domain { get; set; } = "";

        [JsonPropertyName("service")] public string Service { get; set; } = "";

        [JsonPropertyName("service_data")] public JsonElement? ServiceData { get; set; } = null;
    }

    public class HassStateChangedEventData : HassEventData
    {
        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";

        [JsonConverter(typeof(HassStateConverter))]
        [JsonPropertyName("old_state")]
        public HassState? OldState { get; set; } = null;

        [JsonConverter(typeof(HassStateConverter))]
        [JsonPropertyName("new_state")]
        public HassState? NewState { get; set; } = null;
    }

    public class HassState
    {
        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";

        [JsonPropertyName("state")] public dynamic? State { get; set; } = "";

        [JsonPropertyName("attributes")] public Dictionary<string, object>? Attributes { get; set; } = null;

        [JsonPropertyName("last_changed")] public DateTime LastChanged { get; set; } = DateTime.MinValue;

        [JsonPropertyName("last_updated")] public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    }

    public class HassStates : List<HassState>
    {
    }

    public class HassConfig
    {
        [JsonPropertyName("latitude")] public float? Latitude { get; set; } = null;

        [JsonPropertyName("longitude")] public float? Longitude { get; set; } = null;

        [JsonPropertyName("elevation")] public int? Elevation { get; set; } = null;

        [JsonPropertyName("unit_system")] public HassUnitSystem? UnitSystem { get; set; } = null;

        [JsonPropertyName("location_name")] public string? LocationName { get; set; } = null;

        [JsonPropertyName("time_zone")] public string? TimeZone { get; set; } = null;

        [JsonPropertyName("components")] public List<string>? Components { get; set; } = null;

        [JsonPropertyName("config_dir")] public string? ConfigDir { get; set; } = null;

        [JsonPropertyName("whitelist_external_dirs")]
        public List<string>? WhitelistExternalDirs { get; set; } = null;

        [JsonPropertyName("version")] public string? Version { get; set; } = null;
    }

    public class HassUnitSystem
    {
        [JsonPropertyName("length")] public string? Length { get; set; } = null;

        [JsonPropertyName("mass")] public string? Mass { get; set; } = null;

        [JsonPropertyName("temperature")] public string? Temperature { get; set; } = null;

        [JsonPropertyName("volume")] public string? Volume { get; set; } = null;
    }

    #endregion

    #region -- Sending messages --

    public class HassAuthMessage : HassMessageBase
    {
        public HassAuthMessage() => Type = "auth";

        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    }


    public class CommandMessage : HassMessageBase
    {
        [JsonPropertyName("id")] public int Id { get; set; } = 0;
    }

    public class HassPingCommand : CommandMessage
    {
        public HassPingCommand() => Type = "ping";
    }

    public enum EventType
    {
        All = 0,
        HomeAssistantStart = 1,
        HomeAssistantStop = 2,
        StateChanged = 3,
        TimeChanged = 4,
        ServiceRegistered = 5,
        CallService = 6,
        ServiceExecuted = 7,
        PlatformDiscovered = 8,
        ComponentLoaded = 9
    }

    public class SubscribeEventCommand : CommandMessage
    {
        public SubscribeEventCommand() => Type = "subscribe_events";

        [JsonPropertyName("event_type")] public string? EventType { get; set; } = null;
    }

    public class GetStatesCommand : CommandMessage
    {
        public GetStatesCommand() => Type = "get_states";
    }

    public class CallServiceCommand : CommandMessage
    {
        public CallServiceCommand() => Type = "call_service";

        [JsonPropertyName("domain")] public string Domain { get; set; } = "";

        [JsonPropertyName("service")] public string Service { get; set; } = "";

        [JsonPropertyName("service_data")] public object? ServiceData { get; set; } = null;
    }

    public class GetConfigCommand : CommandMessage
    {
        public GetConfigCommand() => Type = "get_config";
    }

    #endregion

    #region -- Json Extensions

    public static class JsonExtensions
    {
        public static T ToObject<T>(this JsonElement element, JsonSerializerOptions? options = null)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                element.WriteTo(writer);
            }

            return JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, options);
        }

        public static HassStates ToHassStates(this JsonElement element, JsonSerializerOptions? options = null)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                element.WriteTo(writer);
            }

            var hassStates = JsonSerializer.Deserialize<HassStates>(bufferWriter.WrittenSpan, options);

            foreach (var hassState in hassStates)
            {
                hassState.State = ((JsonElement)hassState.State).ToDynamicValue();
            }

            return hassStates;
        }

        public static object ParseDataType(string state)
        {
            if (Int64.TryParse(state, NumberStyles.Number, CultureInfo.InvariantCulture, out Int64 intValue))
                return intValue;

            if (Double.TryParse(state, NumberStyles.Number, CultureInfo.InvariantCulture, out Double doubleValue))
                return doubleValue;

            return state;
        }
        public static object? ToDynamicValue(this JsonElement elem)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.String:
                    return ParseDataType(elem.GetString());
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.Number:
                    return elem.TryGetInt64(out Int64 intValue) ? intValue : elem.GetDouble();
                case JsonValueKind.Null:
                    return null;
            }

            return null;
        }
    }

    public class HassStateConverter : JsonConverter<HassState>
    {
        public override HassState Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            HassState m = JsonSerializer.Deserialize<HassState>(ref reader, options);

            JsonElement elem = m.State;

            m.State = elem.ToDynamicValue();

            return m;
        }

        public override void Write(
            Utf8JsonWriter writer,
            HassState value,
            JsonSerializerOptions options) =>
            throw new InvalidOperationException("Serialization not supported for the class EventMessage.");
    }

    public class HassEventConverter : JsonConverter<HassEvent>
    {
        public override HassEvent Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            HassEvent m = JsonSerializer.Deserialize<HassEvent>(ref reader, options);

            switch (m.EventType)
            {
                case "state_changed":
                    m.Data = m.DataElement?.ToObject<HassStateChangedEventData>(options);
                    break;
                case "call_service":
                    m.Data = m.DataElement?.ToObject<HassServiceEventData>(options);
                    break;
            }

            return m;
        }

        public override void Write(
            Utf8JsonWriter writer,
            HassEvent value,
            JsonSerializerOptions options) =>
            throw new InvalidOperationException("Serialization not supported for the class EventMessage.");
    }

    #endregion
}