using System;
using System.Buffers;
using System.Collections.Generic;
using System.Dynamic;
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

    public class HassConfig
    {
        [JsonPropertyName("components")] public List<string>? Components { get; set; } = null;
        [JsonPropertyName("config_dir")] public string? ConfigDir { get; set; } = null;
        [JsonPropertyName("elevation")] public int? Elevation { get; set; } = null;
        [JsonPropertyName("latitude")] public float? Latitude { get; set; } = null;

        [JsonPropertyName("location_name")] public string? LocationName { get; set; } = null;
        [JsonPropertyName("longitude")] public float? Longitude { get; set; } = null;
        [JsonPropertyName("time_zone")] public string? TimeZone { get; set; } = null;
        [JsonPropertyName("unit_system")] public HassUnitSystem? UnitSystem { get; set; } = null;
        [JsonPropertyName("version")] public string? Version { get; set; } = null;

        [JsonPropertyName("whitelist_external_dirs")]
        public List<string>? WhitelistExternalDirs { get; set; } = null;
    }

    public class HassEvent
    {
        public dynamic? Data { get; set; }
        [JsonPropertyName("data")] public JsonElement? DataElement { get; set; } = null;
        [JsonPropertyName("event_type")] public string EventType { get; set; } = "";

        [JsonPropertyName("origin")] public string Origin { get; set; } = "";

        [JsonPropertyName("time_fired")] public DateTime? TimeFired { get; set; } = null;
    }

    public class HassMessage : HassMessageBase
    {
        [JsonPropertyName("event")]
        [JsonConverter(typeof(HassEventConverter))]
        public HassEvent? Event { get; set; }

        [JsonPropertyName("id")] public int Id { get; set; } = 0;

        [JsonPropertyName("message")] public string? Message { get; set; }

        public object? Result { get; set; }
        [JsonPropertyName("result")] public JsonElement? ResultElement { get; set; } = null;
        [JsonPropertyName("success")] public bool? Success { get; set; }
    }
    public class HassServiceEventData
    {
        [JsonPropertyName("domain")] public string Domain { get; set; } = "";

        [JsonPropertyName("service")] public string Service { get; set; } = "";

        [JsonPropertyName("service_data")] public JsonElement? ServiceData { get; set; } = null;
    }

    public class HassState
    {
        [JsonPropertyName("attributes")] public Dictionary<string, object>? Attributes { get; set; } = null;
        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";

        [JsonPropertyName("last_changed")] public DateTime LastChanged { get; set; } = DateTime.MinValue;
        [JsonPropertyName("last_updated")] public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        [JsonPropertyName("state")] public dynamic? State { get; set; } = "";
    }

    public class HassStateChangedEventData
    {
        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";

        [JsonConverter(typeof(HassStateConverter))]
        [JsonPropertyName("new_state")]
        public HassState? NewState { get; set; } = null;

        [JsonConverter(typeof(HassStateConverter))]
        [JsonPropertyName("old_state")]
        public HassState? OldState { get; set; } = null;
    }
    public class HassStates : List<HassState>
    {
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

    public class CallServiceCommand : CommandMessage
    {
        public CallServiceCommand() => Type = "call_service";

        [JsonPropertyName("domain")] public string Domain { get; set; } = "";

        [JsonPropertyName("service")] public string Service { get; set; } = "";

        [JsonPropertyName("service_data")] public object? ServiceData { get; set; } = null;
    }

    public class CommandMessage : HassMessageBase
    {
        [JsonPropertyName("id")] public int Id { get; set; } = 0;
    }

    public class GetConfigCommand : CommandMessage
    {
        public GetConfigCommand() => Type = "get_config";
    }

    public class GetStatesCommand : CommandMessage
    {
        public GetStatesCommand() => Type = "get_states";
    }

    public class HassAuthMessage : HassMessageBase
    {
        public HassAuthMessage() => Type = "auth";

        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    }
    public class HassPingCommand : CommandMessage
    {
        public HassPingCommand() => Type = "ping";
    }
    public class SubscribeEventCommand : CommandMessage
    {
        public SubscribeEventCommand() => Type = "subscribe_events";

        [JsonPropertyName("event_type")] public string? EventType { get; set; } = null;
    }
    #endregion

    #region -- Json Extensions

    public static class JsonExtensions
    {
        public static object ParseDataType(string state)
        {
            if (Int64.TryParse(state, NumberStyles.Number, CultureInfo.InvariantCulture, out Int64 intValue))
                return intValue;

            if (Double.TryParse(state, NumberStyles.Number, CultureInfo.InvariantCulture, out Double doubleValue))
                return doubleValue;

            return state;
        }

        public static dynamic ToDynamic(this JsonElement element)
        {
            dynamic result = new ExpandoObject();
            IDictionary<string, object> dictResult = result;

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var obj in element.EnumerateObject())
                {
                    var jsonElem = obj.Value;
                    dictResult[obj.Name] = obj.Value.ToDynamic();
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                List<dynamic> dynList = new List<dynamic>();
                foreach (var arr in element.EnumerateArray())
                {
                    dynList.Add(arr.ToDynamic());
                }

                result = dynList.ToArray();
            }
            else
            {
                var val = element.ToDynamicValue();
                if (val != null)
                    result = val;
            }

            return result;
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
            }

            return null;
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

        public static T ToObject<T>(this JsonElement element, JsonSerializerOptions? options = null)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                element.WriteTo(writer);
            }

            return JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, options);
        }
    }

    public class HassEventConverter : JsonConverter<HassEvent>
    {
        public override HassEvent Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            HassEvent m = JsonSerializer.Deserialize<HassEvent>(ref reader, options);

            if (m.EventType == "state_changed")
            {
                m.Data = m.DataElement?.ToObject<HassStateChangedEventData>(options);
            }
            else if (m.EventType == "call_service")
            {
                m.Data = m.DataElement?.ToObject<HassServiceEventData>(options);
            }
            else
            {
                m.Data = m.DataElement?.ToDynamic();
            }
            m.DataElement = null;
            return m;
        }

        public override void Write(
            Utf8JsonWriter writer,
            HassEvent value,
            JsonSerializerOptions options) =>
            throw new InvalidOperationException("Serialization not supported for the class EventMessage.");
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
    #endregion
}