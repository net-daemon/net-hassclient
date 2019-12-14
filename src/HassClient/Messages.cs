using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HassClient
{

    public interface IMessageHasId
    {
        int Id { get; set; }
    }
    public class MessageBase
    {

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }
    public class HassMessage : MessageBase
    {
        [JsonPropertyName("id")]
        public int Id { get; set; } = 0;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("success")]
        public bool? Success { get; set; }

        [JsonPropertyName("event")]
        [JsonConverter(typeof(EventMessageConverter))]
        public EventMessage? Event { get; set; }

    }



    public class EventMessage
    {
        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = "";

        [JsonPropertyName("origin")]
        public string Origin { get; set; } = "";

        [JsonPropertyName("time_fired")]
        public DateTime? TimeFired { get; set; } = null;

        [JsonPropertyName("data")]
        public JsonElement? DataElement { get; set; } = null;

        public EventData? Data { get; set; } = null;

    }

    public class EventData
    {

    }


    public class StateChangedEventMessage : EventData
    {
        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; } = "";

        [JsonPropertyName("old_state")]
        public StateMessage? OldState { get; set; } = null;

        [JsonPropertyName("new_state")]
        public StateMessage? NewState { get; set; } = null;
    }

    public class StateMessage
    {
        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; } = "";

        [JsonPropertyName("state")]
        public string State { get; set; } = "";

        [JsonPropertyName("attributes")]
        public Dictionary<string, object>? Attributes { get; set; } = null;

        [JsonPropertyName("last_changed")]
        public DateTime LastChanged { get; set; } = DateTime.MinValue;

        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    }


    public class AuthMessage : MessageBase
    {
        public AuthMessage() => this.Type = "auth";

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
    }

    public class SubscribeEventMessage : MessageBase, IMessageHasId
    {
        public SubscribeEventMessage() => this.Type = "subscribe_events";

        [JsonPropertyName("id")]
        public int Id { get; set; } = 0;

        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }
    }

    public class GetStatesMessage : MessageBase, IMessageHasId
    {
        public GetStatesMessage() => this.Type = "get_states";

        [JsonPropertyName("id")]
        public int Id { get; set; } = 0;
    }

    #region -- Json Extensions

    public static partial class JsonExtensions
    {
        public static T ToObject<T>(this JsonElement element, JsonSerializerOptions? options = null)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
                element.WriteTo(writer);
            return JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, options);
        }

        public static T ToObject<T>(this JsonDocument document, JsonSerializerOptions? options = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            return document.RootElement.ToObject<T>(options);
        }

    }

    /// <summary>
    /// Converter that intercepts EventMessages and serializes the correct data structure
    /// </summary>
    public class EventMessageConverter : JsonConverter<EventMessage>
    {
        public override EventMessage Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {


            var m = JsonSerializer.Deserialize<EventMessage>(ref reader, options);

            switch (m.EventType)
            {
                case "state_changed":
                    m.Data = m.DataElement?.ToObject<StateChangedEventMessage>();
                    break;
            }

            return m;
        }

        public override void Write(
            Utf8JsonWriter writer,
            EventMessage value,
            JsonSerializerOptions options) =>
                throw new InvalidOperationException("Serialization not supported for the class EventMessage.");
    }
    #endregion

}