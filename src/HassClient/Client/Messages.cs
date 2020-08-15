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
        [JsonPropertyName("state")] public string? State { get; set; } = null;
        [JsonPropertyName("whitelist_external_dirs")]
        public List<string>? WhitelistExternalDirs { get; set; } = null;
    }

    public class HassServiceDomain
    {
        public string? Domain { get; set; }
        public IEnumerable<HassService>? Services { get; set; }
    }
    public class HassService
    {
        public string? Service { get; set; }
        public string? Description { get; set; }
        public IEnumerable<HassServiceField>? Fields { get; set; }
    }

    public class HassServiceField
    {
        public string? Field { get; set; }
        public string? Description { get; set; }
        public object? Example { get; set; }
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
        [JsonPropertyName("error")] public HassError? Error { get; set; }
    }

    public class HassError
    {
        [JsonPropertyName("code")] public object? Code { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    public class HassServiceEventData
    {
        [JsonPropertyName("domain")] public string Domain { get; set; } = "";

        [JsonPropertyName("service")] public string Service { get; set; } = "";

        [JsonPropertyName("service_data")] public JsonElement? ServiceData { get; set; } = null;

        public dynamic? Data { get; set; } = null;
    }

    public class HassState
    {
        [JsonPropertyName("attributes")] public Dictionary<string, object>? Attributes { get; set; } = null;
        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";

        [JsonPropertyName("last_changed")] public DateTime LastChanged { get; set; } = DateTime.MinValue;
        [JsonPropertyName("last_updated")] public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        [JsonPropertyName("state")] public dynamic? State { get; set; } = "";
        [JsonPropertyName("context")] public HassContext? Context { get; set; } = null;
    }

    public class HassContext
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("parent_id")] public string? ParentId { get; set; } = null;
        [JsonPropertyName("user_id")] public string? UserId { get; set; } = null;
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


    public class HassAreas : List<HassArea>
    {
    }

    public class HassArea
    {
        [JsonPropertyName("name")] public string? Name { get; set; } = null;
        [JsonPropertyName("area_id")] public string? Id { get; set; } = null;
    }

    public class HassDevices : List<HassDevice>
    {
    }
    public class HassDevice
    {
        [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; } = null;
        [JsonPropertyName("model")] public string? Model { get; set; } = null;
        [JsonPropertyName("id")] public string? Id { get; set; } = null;
        [JsonPropertyName("area_id")] public string? AreaId { get; set; } = null;
        [JsonPropertyName("name")] public string? Name { get; set; } = null;
        [JsonPropertyName("name_by_user")] public string? NameByUser { get; set; } = null;
    }

    public class HassEntities : List<HassEntity>
    {
    }
    public class HassEntity
    {
        [JsonPropertyName("device_id")] public string? DeviceId { get; set; } = null;
        [JsonPropertyName("entity_id")] public string? EntityId { get; set; } = null;
        [JsonPropertyName("name")] public string? Name { get; set; } = null;
        [JsonPropertyName("icon")] public string? Icon { get; set; } = null;
        [JsonPropertyName("platform")] public string? Platform { get; set; } = null;
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

    public class GetServicesCommand : CommandMessage
    {
        public GetServicesCommand() => Type = "get_services";
    }

    //{"type":"config/area_registry/list","id":24}
    public class GetAreasCommand : CommandMessage
    {
        public GetAreasCommand() => Type = "config/area_registry/list";
    }
    //{"type":"config/device_registry/list","id":25}
    public class GetDevicesCommand : CommandMessage
    {
        public GetDevicesCommand() => Type = "config/device_registry/list";
    }

    //{"type":"config/entity_registry/list","id":29}
    public class GetEntitiesCommand : CommandMessage
    {
        public GetEntitiesCommand() => Type = "config/entity_registry/list";
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
        public static object? ParseDataType(string state)
        {
            if (Int64.TryParse(state, NumberStyles.Number, CultureInfo.InvariantCulture, out Int64 intValue))
                return intValue;

            if (Double.TryParse(state, NumberStyles.Number, CultureInfo.InvariantCulture, out Double doubleValue))
                return doubleValue;


            if (state == "unavailable")
                return null;

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


        /// <summary>
        ///     Parses all json elements to instance result from GetServices call
        /// </summary>
        /// <param name="element">JsonElement containing the result data</param>
        public static IEnumerable<HassServiceDomain> ToServicesResult(this JsonElement element)
        {
            var result = new List<HassServiceDomain>();

            if (element.ValueKind != JsonValueKind.Object)
                throw new ApplicationException("Not expected result from the GetServices result");

            foreach (var property in element.EnumerateObject())
            {
                var serviceDomain = new HassServiceDomain
                {
                    Domain = property.Name,
                    Services = getServices(property.Value)
                };
                result.Add(serviceDomain);
            }

            IEnumerable<HassService> getServices(JsonElement element)
            {
                var servicesList = new List<HassService>();
                foreach (var serviceDomainProperty in element.EnumerateObject())
                {
                    servicesList.Add(getServiceFields(serviceDomainProperty.Name, serviceDomainProperty.Value));
                }
                return servicesList;
            }

            HassService getServiceFields(string service, JsonElement element)
            {
                var serviceFields = new List<HassServiceField>();

                var hassService = new HassService
                {
                    Service = service,
                    Fields = serviceFields
                };

                foreach (var serviceProperty in element.EnumerateObject())
                {
                    switch (serviceProperty.Name)
                    {
                        case "description":
                            hassService.Description = serviceProperty.Value.GetString();
                            break;
                        case "fields":
                            foreach (var fieldsProperty in serviceProperty.Value.EnumerateObject())
                            {
                                serviceFields.Add(getField(fieldsProperty.Name, fieldsProperty.Value));
                            }

                            break;
                    }
                }
                return hassService;
            }

            HassServiceField getField(string fieldName, JsonElement element)
            {

                var field = new HassServiceField
                {
                    Field = fieldName
                };
                foreach (var fieldProperty in element.EnumerateObject())
                {
                    switch (fieldProperty.Name)
                    {
                        case "description":
                            field.Description = fieldProperty.Value.GetString();
                            break;
                        case "example":
                            switch (fieldProperty.Value.ValueKind)
                            {
                                case JsonValueKind.String:
                                    field.Example = fieldProperty.Value.GetString();
                                    break;
                                case JsonValueKind.Number:
                                    if (fieldProperty.Value.TryGetInt64(out Int64 longVal))
                                        field.Example = longVal;
                                    else
                                        field.Example = fieldProperty.Value.GetDouble();
                                    break;
                                case JsonValueKind.Object:

                                    field.Example = fieldProperty.Value;
                                    break;
                                case JsonValueKind.True:
                                    field.Example = true;
                                    break;
                                case JsonValueKind.False:
                                    field.Example = false;
                                    break;
                                case JsonValueKind.Array:
                                    field.Example = fieldProperty.Value;
                                    break;
                                default:
                                    break;
                            }
                            break;
                    }
                }
                return field;
            }

            return result;
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
                if (m.Data != null)
                    ((HassServiceEventData)m.Data).Data = ((HassServiceEventData)m.Data).ServiceData?.ToDynamic();
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

            if (m != null)
            {
                JsonElement elem = m.State;

                m.State = elem.ToDynamicValue();

                return m;
            }
            throw new NullReferenceException("Failed to deserialize HassState");

        }

        public override void Write(
            Utf8JsonWriter writer,
            HassState value,
            JsonSerializerOptions options) =>
            throw new InvalidOperationException("Serialization not supported for the class EventMessage.");
    }
    #endregion
}