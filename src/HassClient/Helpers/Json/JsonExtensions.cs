using JoySoftware.HomeAssistant.Model;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;

namespace JoySoftware.HomeAssistant.Helpers.Json
{
    internal static class JsonExtensions
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

        public static JsonElement? ToJsonElement<T>(this T source, JsonSerializerOptions? options = null)
        {
            if (source == null) return null;
            var json = JsonSerializer.Serialize<T>(source, options);
            return JsonDocument.Parse(json).RootElement;
        }

        /// <summary>
        ///     Parses all json elements to instance result from GetServices call
        /// </summary>
        /// <param name="element">JsonElement containing the result data</param>
        public static IReadOnlyCollection<HassServiceDomain> ToServicesResult(this JsonElement element)
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

            IReadOnlyCollection<HassService> getServices(JsonElement element)
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
}