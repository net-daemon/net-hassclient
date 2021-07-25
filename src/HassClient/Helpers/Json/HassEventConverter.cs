using JoySoftware.HomeAssistant.Model;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Helpers.Json
{
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
}