using System;
using System.Text.Json;
using JoySoftware.HomeAssistant.Messages;
using JoySoftware.HomeAssistant.Model;
using Xunit;
using JoySoftware.HomeAssistant.Client;
using JoySoftware.HomeAssistant.Helpers.Json;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace HassClient.Unit.Tests
{
    public class HassMessagesTests
    {
        [Fact]
        public void SerializeHassMessageShouldReturnException()
        {
            // ARRANGE
            var msg = new HassMessage {Id = 1, Success = true, Result = 1, Event = new HassEvent()};

            // ACT AND ASSERT
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(msg));
        }



        [Fact]
        public void DeserializeState_Attributes_ReturnsAttributeDictionary()
        {
            // ARRANGE
            var json = @"
{
    ""event_type"": ""state_changed"",
    ""data"": {
        ""entity_id"": ""switch.dummy_switch"",
        ""old_state"": {
            ""entity_id"": ""switch.dummy_switch"",
            ""state"": ""off"",
            ""attributes"": {
                ""friendly_name"": ""dummy_switch"",
                ""assumed_state"": true
            },
            ""last_changed"": ""2021-05-08T20:43:02.116327+00:00"",
            ""last_updated"": ""2021-05-08T20:43:02.116327+00:00"",
            ""context"": {
                ""id"": ""5c7bbeab8b11d2310f8ebe44ae7365cf"",
                ""parent_id"": null,
                ""user_id"": null
            }
        },
        ""new_state"": {
            ""entity_id"": ""switch.dummy_switch"",
            ""state"": ""on"",
            ""attributes"": {
                ""friendly_name"": ""dummy_switch"",
                ""assumed_state"": true
            },
            ""last_changed"": ""2021-05-08T20:43:28.277645+00:00"",
            ""last_updated"": ""2021-05-08T20:43:28.277645+00:00"",
            ""context"": {
                ""id"": ""c5e0225ac594be20a9c5bb92800885a2"",
                ""parent_id"": null,
                ""user_id"": null
            }
        }
    },
    ""origin"": ""LOCAL"",
    ""time_fired"": ""2021-05-08T20:43:28.277645+00:00"",
    ""context"": {
        ""id"": ""c5e0225ac594be20a9c5bb92800885a2"",
        ""parent_id"": null,
        ""user_id"": null
    }
}";
            var reader = new Utf8JsonReader(new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(json)));

            // ACT:
            var result = new HassEventConverter().Read(ref reader, typeof(string), new JsonSerializerOptions());

            var statechangedEvent = result.Data as HassStateChangedEventData;
            var newState = statechangedEvent.NewState;

            var untypedAttributes = newState.Attributes;

            // ASSERT
            Assert.Equal("dummy_switch", (untypedAttributes["friendly_name"] as JsonElement?)?.GetString());
            Assert.Equal(true, (untypedAttributes["assumed_state"] as JsonElement?)?.GetBoolean());
        }

        [Fact]
        public void DeserializeState_AttributesAs_ReturnsTypedAttributes()
        {
            // ARRANGE
            var json = @"
{
    ""event_type"": ""state_changed"",
    ""data"": {
        ""entity_id"": ""switch.netdaemon_openwindowclimateoff"",
        ""old_state"": {
            ""entity_id"": ""switch.netdaemon_openwindowclimateoff"",
            ""state"": ""on"",
            ""attributes"": {
                ""runtime_info"": {
                    ""next_scheduled_event"": ""2021-05-08T15:25:02.4237268+02:00"",
                    ""has_error"": false,
                    ""app_attributes"": {}
                }
            },
            ""last_changed"": ""2021-05-05T19:27:46.567476+00:00"",
            ""last_updated"": ""2021-05-08T13:24:02.425779+00:00"",
            ""context"": {
                ""id"": ""d8e56d574352c8fbe51b1cdfdaaca7d1"",
                ""parent_id"": null,
                ""user_id"": ""c1bd80bacf0f432d9da6dce61eb7deb9""
            }
        },
        ""new_state"": {
            ""entity_id"": ""switch.netdaemon_openwindowclimateoff"",
            ""state"": ""on"",
            ""attributes"": {
                ""runtime_info"": {
                    ""next_scheduled_event"": ""2021-05-08T15:26:02.4236657+02:00"",
                    ""has_error"": false,
                    ""app_attributes"": {}
                }
            },
            ""last_changed"": ""2021-05-05T19:27:46.567476+00:00"",
            ""last_updated"": ""2021-05-08T13:25:02.425548+00:00"",
            ""context"": {
                ""id"": ""741df30c8cc7f58dc26b40be567cc13f"",
                ""parent_id"": null,
                ""user_id"": ""c1bd80bacf0f432d9da6dce61eb7deb9""
            }
        }
    },
    ""origin"": ""LOCAL"",
    ""time_fired"": ""2021-05-08T13:25:02.425548+00:00"",
    ""context"": {
        ""id"": ""741df30c8cc7f58dc26b40be567cc13f"",
        ""parent_id"": null,
        ""user_id"": ""c1bd80bacf0f432d9da6dce61eb7deb9""
    }
}
";
            var reader = new Utf8JsonReader(new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(json)));

            // ACT:
            var result = new HassEventConverter().Read(ref reader, typeof(string), new JsonSerializerOptions());

            var statechangedEvent = result.Data as HassStateChangedEventData;
            var newState = statechangedEvent.NewState;

            var typedAttributes = newState.AttributesAs<DaemonAppAttributes>();

            // ASSERT
            var expectedDateTime = DateTimeOffset.Parse("2021-05-08T15:26:02.4236657+02:00");

            Assert.Equal(expectedDateTime, typedAttributes.RuntimeInfo.NextScheduledEvent);
            Assert.False(typedAttributes.RuntimeInfo.HasError);
            Assert.NotNull(typedAttributes.RuntimeInfo);

            var runtimeInfo = newState.Attributes["runtime_info"] as Dictionary<string, object>;

            Assert.IsType<JsonElement>(runtimeInfo["next_scheduled_event"]);
            Assert.IsType<JsonElement>(runtimeInfo["has_error"]);
            Assert.IsType<JsonElement>(runtimeInfo["app_attributes"] is ICollection);


        }

        record RuntimeInfo
        {
            [JsonPropertyName("next_scheduled_event")] public DateTimeOffset NextScheduledEvent { get; init; }
            [JsonPropertyName("has_error")] public bool HasError { get; init; }
            [JsonPropertyName("app_attributes")] public object AppAttributes { get; init; }
        }

        record DaemonAppAttributes
        {
            [JsonPropertyName("runtime_info")] public RuntimeInfo RuntimeInfo { get; init; }
        }
    }
}