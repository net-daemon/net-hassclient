using JoySoftware.HomeAssistant.Model;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Helpers.Json
{
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
}