using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Client
{
    public record HassMessageBase
    {
        [JsonPropertyName("type")] public string Type { get; init; } = "";
    }

    #region -- Receiving messages --

    [SuppressMessage("", "CA2227")]
    public record HassConfig
    {
        [JsonPropertyName("components")] public IList<string>? Components { get; init; }
        [JsonPropertyName("config_dir")] public string? ConfigDir { get; init; }
        [JsonPropertyName("elevation")] public int? Elevation { get; init; }
        [JsonPropertyName("latitude")] public float? Latitude { get; init; }
        [JsonPropertyName("location_name")] public string? LocationName { get; init; }
        [JsonPropertyName("longitude")] public float? Longitude { get; init; }
        [JsonPropertyName("time_zone")] public string? TimeZone { get; init; }
        [JsonPropertyName("unit_system")] public HassUnitSystem? UnitSystem { get; init; }
        [JsonPropertyName("version")] public string? Version { get; init; }
        [JsonPropertyName("state")] public string? State { get; init; }
        [JsonPropertyName("whitelist_external_dirs")]
        public IList<string>? WhitelistExternalDirs { get; init; }
    }

    public record HassServiceDomain
    {
        public string? Domain { get; init; }
        public IEnumerable<HassService>? Services { get; init; }
    }
    public record HassService
    {
        public string? Service { get; init; }
        public string? Description { get; init; }
        public IEnumerable<HassServiceField>? Fields { get; init; }
    }

    public record HassServiceField
    {
        public string? Field { get; init; }
        public string? Description { get; init; }
        public object? Example { get; init; }
    }

    public record HassEvent
    {
        public dynamic? Data { get; set; }
        [JsonPropertyName("data")] public JsonElement? DataElement { get; init; }
        [JsonPropertyName("event_type")]
        public string EventType { get; init; } = "";

        [JsonPropertyName("origin")] public string Origin { get; init; } = "";

        [JsonPropertyName("time_fired")] public DateTime? TimeFired { get; init; }
    }

    public record HassMessage : HassMessageBase
    {
        [JsonPropertyName("event")]
        [JsonConverter(typeof(HassEventConverter))]
        public HassEvent? Event { get; init; }
        [JsonPropertyName("id")] public int Id { get; init; }
        [JsonPropertyName("message")] public string? Message { get; init; }
        public object? Result { get; set; }
        [JsonPropertyName("result")] public JsonElement? ResultElement { get; init; }
        [JsonPropertyName("success")] public bool? Success { get; init; }
        [JsonPropertyName("error")] public HassError? Error { get; init; }
    }

    public record HassError
    {
        [JsonPropertyName("code")] public object? Code { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    [SuppressMessage("", "CA2227")]
    public record HassServiceEventData
    {
        [JsonPropertyName("domain")] public string Domain { get; set; } = "";

        [JsonPropertyName("service")] public string Service { get; set; } = "";

        [JsonPropertyName("service_data")] public JsonElement? ServiceData { get; set; }

        public dynamic? Data { get; set; }
    }

    [SuppressMessage("", "CA2227")]
    public record HassState
    {
        [JsonPropertyName("attributes")] public Dictionary<string, object>? Attributes { get; set; }
        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";

        [JsonPropertyName("last_changed")] public DateTime LastChanged { get; set; } = DateTime.MinValue;
        [JsonPropertyName("last_updated")] public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        [JsonPropertyName("state")] public dynamic? State { get; set; } = "";
        [JsonPropertyName("context")] public HassContext? Context { get; set; }
    }

    public record HassContext
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("parent_id")] public string? ParentId { get; set; }
        [JsonPropertyName("user_id")] public string? UserId { get; set; }
    }

    public record HassStateChangedEventData
    {
        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";

        [JsonConverter(typeof(HassStateConverter))]
        [JsonPropertyName("new_state")]
        public HassState? NewState { get; set; }

        [JsonConverter(typeof(HassStateConverter))]
        [JsonPropertyName("old_state")]
        public HassState? OldState { get; set; }
    }
    public class HassStates : List<HassState>
    {
    }
    public record HassUnitSystem
    {
        [JsonPropertyName("length")] public string? Length { get; set; }
        [JsonPropertyName("mass")] public string? Mass { get; set; }

        [JsonPropertyName("temperature")] public string? Temperature { get; set; }

        [JsonPropertyName("volume")] public string? Volume { get; set; }
    }

    public class HassAreas : List<HassArea>
    {
    }

    public record HassArea
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("area_id")] public string? Id { get; set; }
    }

    public class HassDevices : List<HassDevice>
    {
    }
    public record HassDevice
    {
        [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("area_id")] public string? AreaId { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("name_by_user")] public string? NameByUser { get; set; }
    }

    public class HassEntities : List<HassEntity>
    {
    }

    public record HassEntity
    {
        [JsonPropertyName("device_id")] public string? DeviceId { get; set; }
        [JsonPropertyName("entity_id")] public string? EntityId { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("icon")] public string? Icon { get; set; }
        [JsonPropertyName("platform")] public string? Platform { get; set; }
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

    public record CallServiceCommand : CommandMessage
    {
        public CallServiceCommand() => Type = "call_service";

        [JsonPropertyName("domain")] public string Domain { get; set; } = "";

        [JsonPropertyName("service")] public string Service { get; set; } = "";

        [JsonPropertyName("service_data")] public object? ServiceData { get; set; }
    }

    public record CommandMessage : HassMessageBase
    {
        [JsonPropertyName("id")] public int Id { get; set; }
    }

    public record GetConfigCommand : CommandMessage
    {
        public GetConfigCommand() => Type = "get_config";
    }

    public record GetStatesCommand : CommandMessage
    {
        public GetStatesCommand() => Type = "get_states";
    }

    public record GetServicesCommand : CommandMessage
    {
        public GetServicesCommand() => Type = "get_services";
    }

    //{"type":"config/area_registry/list","id":24}
    public record GetAreasCommand : CommandMessage
    {
        public GetAreasCommand() => Type = "config/area_registry/list";
    }
    //{"type":"config/device_registry/list","id":25}
    public record GetDevicesCommand : CommandMessage
    {
        public GetDevicesCommand() => Type = "config/device_registry/list";
    }

    //{"type":"config/entity_registry/list","id":29}
    public record GetEntitiesCommand : CommandMessage
    {
        public GetEntitiesCommand() => Type = "config/entity_registry/list";
    }

    public record HassAuthMessage : HassMessageBase
    {
        public HassAuthMessage() => Type = "auth";

        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    }
    public record HassPingCommand : CommandMessage
    {
        public HassPingCommand() => Type = "ping";
    }
    public record SubscribeEventCommand : CommandMessage
    {
        public SubscribeEventCommand() => Type = "subscribe_events";

        [JsonPropertyName("event_type")] public string? EventType { get; set; }
    }
    #endregion

    #region -- Json Extensions

    public static class JsonExtensions
    {
        private static object? ParseDataType(string? state)
        {
            if (long.TryParse(state, NumberStyles.Number, CultureInfo.InvariantCulture, out long intValue))
                return intValue;

            if (double.TryParse(state, NumberStyles.Number, CultureInfo.InvariantCulture, out double doubleValue))
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
            return elem.ValueKind switch
            {
                JsonValueKind.String => ParseDataType(elem.GetString()),
                JsonValueKind.False => false,
                JsonValueKind.True => true,
                JsonValueKind.Number => elem.TryGetInt64(out long intValue) ? intValue : elem.GetDouble(),
                _ => null,
            };
        }

        public static HassStates ToHassStates(this JsonElement element, JsonSerializerOptions? options = null)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                element.WriteTo(writer);
            }

            var hassStates = JsonSerializer.Deserialize<HassStates>(bufferWriter.WrittenSpan, options)
                ?? throw new ApplicationException("Hass states desrialization resulted in empty result");

            foreach (var hassState in hassStates)
            {
                hassState.State = ((JsonElement)hassState.State).ToDynamicValue();
            }

            return hassStates;
        }

        [return: MaybeNull]
        public static T ToObject<T>(this JsonElement element, JsonSerializerOptions? options = null)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                element.WriteTo(writer);
            }

            return JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, options) ?? default!;
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

                string? serviceDescription = null;

                foreach (var serviceProperty in element.EnumerateObject())
                {
                    switch (serviceProperty.Name)
                    {
                        case "description":
                            serviceDescription = serviceProperty.Value.GetString();
                            break;
                        case "fields":
                            foreach (var fieldsProperty in serviceProperty.Value.EnumerateObject())
                            {
                                serviceFields.Add(getField(fieldsProperty.Name, fieldsProperty.Value));
                            }

                            break;
                    }
                }
                return new HassService
                {
                    Service = service,
                    Fields = serviceFields,
                    Description = serviceDescription
                };
            }

            HassServiceField getField(string fieldName, JsonElement element)
            {
                object? example = null;
                string? fieldDescription = null;
                foreach (var fieldProperty in element.EnumerateObject())
                {
                    switch (fieldProperty.Name)
                    {
                        case "description":
                            fieldDescription = fieldProperty.Value.GetString();
                            break;
                        case "example":
                            switch (fieldProperty.Value.ValueKind)
                            {
                                case JsonValueKind.String:
                                    example = fieldProperty.Value.GetString();
                                    break;
                                case JsonValueKind.Number:
                                    if (fieldProperty.Value.TryGetInt64(out long longVal))
                                        example = longVal;
                                    else
                                        example = fieldProperty.Value.GetDouble();
                                    break;
                                case JsonValueKind.Object:

                                    example = fieldProperty.Value;
                                    break;
                                case JsonValueKind.True:
                                    example = true;
                                    break;
                                case JsonValueKind.False:
                                    example = false;
                                    break;
                                case JsonValueKind.Array:
                                    example = fieldProperty.Value;
                                    break;
                            }
                            break;
                    }
                }
                return new HassServiceField
                {
                    Field = fieldName,
                    Example = example,
                    Description = fieldDescription,
                };
            }

            return result;
        }
    }

    public class HassEventConverter : JsonConverter<HassEvent>
    {
        public override HassEvent? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            HassEvent m = JsonSerializer.Deserialize<HassEvent>(ref reader, options)
                ?? throw new ApplicationException("HassEvent is empty!");

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
            HassState m = JsonSerializer.Deserialize<HassState>(ref reader, options)
                ?? throw new ApplicationException("HassState is empty");

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