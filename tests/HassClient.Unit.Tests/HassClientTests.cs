using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JoySoftware.HomeAssistant.Client;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace HassClient.Unit.Tests
{
    public class HassClientTests
    {
        [Theory]
        [InlineData(EventType.All)]
        [InlineData(EventType.ServiceRegistered)]
        [InlineData(EventType.CallService)]
        [InlineData(EventType.ComponentLoaded)]
        [InlineData(EventType.HomeAssistantStart)]
        [InlineData(EventType.PlatformDiscovered)]
        [InlineData(EventType.ServiceExecuted)]
        [InlineData(EventType.StateChanged)]
        [InlineData(EventType.TimeChanged)]
        [InlineData(EventType.HomeAssistantStop)]
        public async Task SubscriptToEventTypeShouldReturnEvent(EventType eventType)
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // ACT AND ASSERT
            var subscribeTask = hassClient.SubscribeToEvents(eventType);
            mock.AddResponse(@"{""id"": 2, ""type"": ""result"", ""success"": true, ""result"": null}");
            Assert.True(await subscribeTask);
            mock.AddResponse(HassWebSocketMock.EventMessage);
            HassEvent eventMsg = await hassClient.ReadEventAsync();
            Assert.NotNull(eventMsg);
        }

        [Theory]
        [InlineData(WebSocketState.Closed)]
        [InlineData(WebSocketState.Aborted)]
        [InlineData(WebSocketState.CloseReceived)]
        [InlineData(WebSocketState.CloseSent)]
        [InlineData(WebSocketState.Connecting)]
        [InlineData(WebSocketState.None)]
        public async Task ConnectWithStateOtherThanOpenShouldReturnFalse(WebSocketState state)
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default state hass client
            var hassClient = mock.GetHassClient();
            // Set Closed state to fake
            mock.SetupGet(x => x.State).Returns(state);

            // ACT and ASSERT
            Assert.False(await hassClient.ConnectAsync(new Uri("ws://anyurldoesntmatter.org"), "FAKETOKEN", false));
        }

        public class UnknownCommand : CommandMessage
        {
            public UnknownCommand() => Type = "unknown_command";
        }

        [Fact]
        public async Task CallServiceIfCanceledShouldThrowOperationCanceledException()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // Do not add a fake service call message result

            // ACT
            var callServiceTask = hassClient.CallService("light", "turn_on", new { entity_id = "light.tomas_rum" });
            hassClient.CancelSource.Cancel();

            // ASSERT
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await callServiceTask);
        }

        [Fact]
        public async Task CallServiceSuccessfulReturnsTrue()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // Service call successful
            mock.AddResponse(@"{
                                      ""id"": 2,
                                      ""type"": ""result"",
                                      ""success"": true,
                                      ""result"": {
                                        ""context"": {
                                          ""id"": ""55cf75a4dbf94680804ef022aa0c67b4"",
                                          ""parent_id"": null,
                                          ""user_id"": ""63b2952cb986474d84be46480c8aaad3""
                                        }
                                      }
                                    }");

            // ACT
            var result = await hassClient.CallService("light", "turn_on", new { entity_id = "light.tomas_rum" });

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CallServiceWithoutResponseShouldReturnSuccessWitoutReturnMessage()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // Service call successful
            mock.AddResponse(@"{
                                      ""id"": 2,
                                      ""type"": ""result"",
                                      ""success"": true,
                                      ""result"": {
                                        ""context"": {
                                          ""id"": ""55cf75a4dbf94680804ef022aa0c67b4"",
                                          ""parent_id"": null,
                                          ""user_id"": ""63b2952cb986474d84be46480c8aaad3""
                                        }
                                      }
                                    }");

            // ACT
            var result = await hassClient.CallService("light", "turn_on", new { entity_id = "light.tomas_rum" }, false);

            // Assert
            Assert.True(result);

        }

        //[Fact]
        //public async Task CallServiceUnhandledErrorThrowsException()
        //{
        //    // ARRANGE
        //    var webSocketMock = Mock.Of<IClientWebSocket>(ws =>
        //        ws.SendAsync(null, WebSocketMessageType.Text, true, CancellationToken.None) ==
        //        Task.FromException(new Exception("Some exception")));

        //    var factoryMock = Mock.Of<IClientWebSocketFactory>(mf =>
        //        mf.New() == webSocketMock
        //    );

        //    var hc = new JoySoftware.HomeAssistant.Client.HassClient(null, factoryMock);

        //    // ACT AND ASSERTs
        //    await Assert.ThrowsAsync<OperationCanceledException>(async () => await hc.SubscribeToEvents());
        //}

        [Fact]
        public async Task CallServiceWithTimeoutShouldReturnFalse()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();
            hassClient.SocketTimeout = 10;

            // ACT AND ASSERT

            // Do not add a message and force timeout
            Assert.False(await hassClient.CallService("light", "turn_on", new { entity_id = "light.tomas_rum" }));
        }

        [Fact]
        public async Task ClientGetUnexpectedMessageRecoversResultNotNull()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // ACT
            var confTask = hassClient.GetConfig();

            // First add an unexpected message, message id should be 2
            mock.AddResponse(@"{""id"": 12345, ""type"": ""result"", ""success"": false, ""result"": null}");
            // Then add the expected one... It should recover from that...
            mock.AddResponse(HassWebSocketMock.ConfigMessage);

            // ASSERT
            Assert.NotNull(await confTask);
        }

        [Fact]
        public async Task CloseAsyncIsRanOnce()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            await using IHassClient hassClient = await mock.GetHassConnectedClient();

            await hassClient.CloseAsync();

            // ASSERT
            mock.Verify(
                x => x.CloseOutputAsync(It.IsAny<WebSocketCloseStatus>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        //TODO: Fix the test
        [Fact]
        public async Task CloseAsyncWithTimeoutThrowsOperationCanceledExceotion()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            mock.Setup(x =>
                    x.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromException<OperationCanceledException>(new OperationCanceledException("Fake close")));

            hassClient.SocketTimeout = 20;

            // ACT
            await hassClient.CloseAsync();

            // ASSERT
            mock.Logger.AssertLogged(LogLevel.Trace, Times.AtLeastOnce());
        }

        [Fact]
        public async Task CommandWithUnsuccessfulShouldThrowAggregateException()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // ACT
            Task<HassConfig> confTask = hassClient.GetConfig();

            // Add result not success message
            mock.AddResponse(@"{""id"": 2, ""type"": ""result"", ""success"": false, ""result"": null}");

            // ASSERT
            Assert.Throws<AggregateException>(() => confTask.Result);
        }

        [Fact]
        public async Task ConfigShouldBeCorrect()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // ACT
            Task<HassConfig> getConfigTask = hassClient.GetConfig();
            // Fake return Config message, check result_config.json for reference
            mock.AddResponse(HassWebSocketMock.ConfigMessage);

            var conf = getConfigTask.Result;

            // ASSERT, its an object assertion here so multiple asserts allowed
            // Check result_config.json for reference
            Assert.NotNull(conf);
            Assert.Equal("°C", conf.UnitSystem?.Temperature);
            Assert.Equal("km", conf.UnitSystem?.Length);
            Assert.Equal("g", conf.UnitSystem?.Mass);
            Assert.Equal("L", conf.UnitSystem?.Volume);

            Assert.Contains("binary_sensor.deconz", conf.Components);
            Assert.Equal(62.2398549F, conf.Latitude);
            Assert.Equal(15.4412267F, conf.Longitude);
            Assert.Equal(49, conf.Elevation);

            Assert.Contains("/config/www", conf.WhitelistExternalDirs);
            Assert.Equal("0.87.0", conf.Version);
            Assert.Equal("Home", conf.LocationName);

            Assert.Equal("/config", conf.ConfigDir);
            Assert.Equal("Europe/Stockholm", conf.TimeZone);
        }

        [Fact]
        public async Task ConnectAlreadyConnectedThrowsInvalidOperation()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // ACT AND ASSERT

            // The hass client already connected and should assert error
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await hassClient.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));
        }

        [Fact]
        public async Task ConnectShouldReturnTrue()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default state hass client
            var hassClient = mock.GetHassClient();

            // First message from Home Assistant is auth required
            mock.AddResponse(@"{""type"": ""auth_required""}");
            // Next one we fake it is auth ok
            mock.AddResponse(@"{""type"": ""auth_ok""}");

            // ACT and ASSERT
            // Calls connect without getting the states initially
            Assert.True(await hassClient.ConnectAsync(new Uri("ws://anyurldoesntmatter.org"), "FAKETOKEN", false));
        }

        [Fact]
        public async Task ConnectTimeoutReturnsFalse()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default state hass client and we add no response messages
            var hassClient = mock.GetHassClient();

            // Set the timeout to a very low value for testing purposes
            hassClient.SocketTimeout = 20;

            // ACT AND ASSERT
            Assert.False(await hassClient.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));
        }

        [Fact]
        public async Task ConnectWithAuthFailLogsErrorAndReturnFalse()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default state hass client
            var hassClient = mock.GetHassClient();

            // First message from Home Assistant is auth required
            mock.AddResponse(@"{""type"": ""auth_required""}");
            // Next one we fake it is auth ok
            mock.AddResponse(@"{""type"": ""auth_invalid""}");

            // ACT and ASSERT
            // Calls connect without getting the states initially
            Assert.False(await hassClient.ConnectAsync(new Uri("ws://anyurldoesntmatter.org"), "FAKETOKEN", false));
            // Make sure we logged the error.
            mock.Logger.AssertLogged(LogLevel.Error, Times.AtLeastOnce());
        }

        [Fact]
        public async Task ConnectWithoutSslShouldStartWithWs()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default state hass client and we add no response messages
            var hassClient = mock.GetHassClient();
            // First message from Home Assistant is auth required
            mock.AddResponse(@"{""type"": ""auth_required""}");
            // Next one we fake it is auth ok
            mock.AddResponse(@"{""type"": ""auth_ok""}");

            // ACT and ASSERT
            // Connect without ssl
            await hassClient.ConnectAsync("localhost", 8123, false, "FAKETOKEN", false);

            mock.Verify(
                n => n.ConnectAsync(new Uri("ws://localhost:8123/api/websocket"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ConnectWithSslShouldStartWithWss()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default state hass client and we add no response messages
            var hassClient = mock.GetHassClient();
            // First message from Home Assistant is auth required
            mock.AddResponse(@"{""type"": ""auth_required""}");
            // Next one we fake it is auth ok
            mock.AddResponse(@"{""type"": ""auth_ok""}");

            // ACT and ASSERT
            // Connect with ssl
            await hassClient.ConnectAsync("localhost", 8123, true, "FAKETOKEN", false);

            mock.Verify(
                n => n.ConnectAsync(new Uri("wss://localhost:8123/api/websocket"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ConnectWithUriNullThrowsArgumentNullException()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default state hass client and we add no response messages
            var hassClient = mock.GetHassClient();

            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await hassClient.ConnectAsync(null, "lss", false));
        }

        [Fact]
        public async Task CustomEventShouldHaveCorrectObject()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // Add the service message fake , check service_event.json for reference
            mock.AddResponse(HassWebSocketMock.CustomEventMessage);

            // ACT
            var result = await hassClient.ReadEventAsync();
            var customEvent = result?.Data;


            // ASSERT
            Assert.Equal("light.some_light", customEvent?.an_object.entity_id);
            Assert.IsType<object[]>(customEvent?.an_object.value_array);
            var x = customEvent?.an_object.value_array[0];

            Assert.Equal(1, x);
        }

        [Fact]
        public async Task EventWithStateBooleanShouldHaveCorrectTypeAndValue()
        {
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // Add response event message, see event.json as reference
            mock.AddResponse(HassWebSocketMock.EventMessageBoolean);

            // ACT
            HassEvent eventMsg = await hassClient.ReadEventAsync();

            var stateMessage = eventMsg.Data as HassStateChangedEventData;

            Assert.Equal(true, stateMessage?.NewState.State);
            Assert.Equal(false, stateMessage?.OldState.State);
        }

        [Fact]
        public async Task EventWithStateDoubleShouldHaveCorrectTypeAndValue()
        {
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // Add response event message, see event.json as reference
            mock.AddResponse(HassWebSocketMock.EventMessageDouble);

            // ACT
            HassEvent eventMsg = await hassClient.ReadEventAsync();

            var stateMessage = eventMsg.Data as HassStateChangedEventData;

            Assert.Equal(3.21, stateMessage?.NewState.State);
            Assert.Equal(1.23, stateMessage?.OldState.State);
        }


        [Fact]
        public async Task EventWithStateIntegerShouldHaveCorrectTypeAndValue()
        {
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // Add response event message, see event.json as reference
            mock.AddResponse(HassWebSocketMock.EventMessageInteger);

            // ACT
            HassEvent eventMsg = await hassClient.ReadEventAsync();

            var stateMessage = eventMsg.Data as HassStateChangedEventData;

            Assert.Equal(321, stateMessage?.NewState.State);
            Assert.Equal(123, stateMessage?.OldState.State);
        }

        [Fact]
        public async Task GetConfigGetUnexpectedMessageThrowsException()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // ACT
            var getConfigTask = hassClient.GetConfig();

            // Fake return not expected message, check result_config.json for reference
            mock.AddResponse(@"{""id"": 2,""type"": ""result"", ""success"": true}");

            await Assert.ThrowsAsync<ApplicationException>(async () => await getConfigTask);
        }

        [Fact]
        public async Task GetConfigGetUnexpectedResultThrowsException()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            var mockHassClient =
                new Mock<JoySoftware.HomeAssistant.Client.HassClient>(mock.Logger.LoggerFactory,
                    mock.WebSocketMockFactory.Object, null);


            mockHassClient.CallBase = true;

            // First message from Home Assistant is auth required
            mock.AddResponse(@"{""type"": ""auth_required""}");
            // Next one we fake it is auth ok
            mock.AddResponse(@"{""type"": ""auth_ok""}");

            await mockHassClient.Object.ConnectAsync(new Uri("http://192.168.1.1"), "token", false);

            mockHassClient.Setup(n =>
                    n.SendCommandAndWaitForResponse(
                        It.IsAny<CommandMessage>(), It.IsAny<bool>()))
                    .Returns(
                        new ValueTask<HassMessage>(new HassMessage
                        {
                            Id = 2,
                            Type = "result",
                            Result = "Not correct type as we should test"
                        }));

            // ACT AND ASSERT
            var getConfigTask = mockHassClient.Object.GetConfig();

            await Assert.ThrowsAsync<ApplicationException>(async () => await getConfigTask);
        }

        [Fact]
        public async Task NoPongMessagePingShouldReturnFalse()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // No pong message is sent from server...

            // ACT and ASSERT
            Assert.False(await hassClient.PingAsync(2));
        }

        [Fact]
        public async Task PingShouldReturnTrue()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // Fake return pong message
            mock.AddResponse(@"{""type"": ""pong""}");

            // ACT and ASSERT
            Assert.True(await hassClient.PingAsync(1000));
        }

        [Fact]
        public async Task ReceiveAsyncThrowsExceptionProcessMessageShouldHandleException()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            mock.Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                .Returns((Memory<byte> buffer, CancellationToken token) =>
                {
                    throw new Exception("Unexpected!");
                });


            // ACT AND ASSERT
            var subscribeTask = hassClient.SubscribeToEvents();

            // Service call successful
            mock.AddResponse(@"{
                                      ""id"": 2,
                                      ""type"": ""result"",
                                      ""success"": true,
                                      ""result"": {
                                        ""context"": {
                                          ""id"": ""55cf75a4dbf94680804ef022aa0c67b4"",
                                          ""parent_id"": null,
                                          ""user_id"": ""63b2952cb986474d84be46480c8aaad3""
                                        }
                                      }
                                    }");
            await Task.Delay(20);
            await subscribeTask;

            mock.Logger.AssertLogged(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task ReturningStatesTheCountShouldBeNineteen()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the non connected hass client
            var hassClient = mock.GetHassClientNotConnected();

            hassClient.SocketTimeout = 50000;
            // ACT

            var connectTask = hassClient.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN");

            // Wait until hassclient processes connect sequence
            await mock.WaitUntilConnected();

            // Fake return states message
            mock.AddResponse(HassWebSocketMock.StateMessage);
            await connectTask;

            // ASSERT
            Assert.Equal(19, hassClient.States.Count);
        }

        [Fact]
        public async Task SendingUnknownMessageShouldDiscardAndLogDebug()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            hassClient.SendMessage(new UnknownCommand());
            hassClient.SocketTimeout = 20;
            await hassClient.CallService("test", "test", null);
            mock.Logger.AssertLogged(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task SendMessageFailShouldThrowException()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            var mockHassClient =
                new Mock<JoySoftware.HomeAssistant.Client.HassClient>(mock.Logger.LoggerFactory,
                    mock.WebSocketMockFactory.Object, null);


            mockHassClient.CallBase = true;

            // First message from Home Assistant is auth required
            mock.AddResponse(@"{""type"": ""auth_required""}");
            // Next one we fake it is auth ok
            mock.AddResponse(@"{""type"": ""auth_ok""}");

            await mockHassClient.Object.ConnectAsync(new Uri("http://192.168.1.1"), "token", false);
            mockHassClient.Setup(n => n.SendMessage(It.IsAny<HassMessageBase>(),
                It.IsAny<bool>())).Returns(false);
            // ACT AND ASSERT

            await Assert.ThrowsAsync<ApplicationException>(async () =>
                await mockHassClient.Object.CallService("light", "turn_on", null));
        }

        [Fact]
        public async Task ServiceEventShouldHaveCorrectObject()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // Add the service message fake , check service_event.json for reference
            mock.AddResponse(HassWebSocketMock.ServiceMessage);

            // ACT
            var result = await hassClient.ReadEventAsync();
            var serviceEvent = result?.Data as HassServiceEventData;
            JsonElement? c = serviceEvent?.ServiceData?.GetProperty("entity_id");

            // ASSERT
            Assert.NotNull(serviceEvent);
            Assert.Equal("light", serviceEvent.Domain);
            Assert.Equal("toggle", serviceEvent.Service!);
            Assert.Equal("light.tomas_rum", c?.GetString());
            Assert.Equal("light.tomas_rum", serviceEvent.Data.entity_id);

        }

        [Fact]
        public async Task GetServiceShouldHaveCorrectObject()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            var task = hassClient.GetServices();
            // Add the service message fake , check service_event.json for reference
            mock.AddResponse(HassWebSocketMock.GetServiceMessage);

            // ACT
            // HassEvent eventMsg = await hassClient.ReadEventAsync();
            var result = await task;

            var first = result.FirstOrDefault();

            // ASSERT
            Assert.NotNull(result);
            Assert.NotNull(first);
            Assert.Equal("homeassistant", first.Domain);

            Assert.Equal(38, result.Count());
            // Assert.Equal("light", serviceEvent.Domain);
            // Assert.Equal("toggle", serviceEvent.Service!);
            // Assert.Equal("light.tomas_rum", c?.GetString());
            // Assert.Equal("light.tomas_rum", serviceEvent.Data.entity_id);

        }


        [Fact]
        public async Task SubscribeToEventsReturnsCorrectEvent()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            var subscribeTask = hassClient.SubscribeToEvents();
            // Add result success
            mock.AddResponse(@"{""id"": 2, ""type"": ""result"", ""success"": true, ""result"": null}");
            await subscribeTask;

            // Add response event message, see event.json as reference
            mock.AddResponse(HassWebSocketMock.EventMessage);

            // ACT
            HassEvent eventMsg = await hassClient.ReadEventAsync();

            // ASSERT, object multiple assertions
            Assert.NotNull(eventMsg);

            Assert.Equal("LOCAL", eventMsg.Origin);
            Assert.Equal(DateTime.Parse("2019-02-17T11:43:47.090511+00:00"), eventMsg.TimeFired);

            var stateMessage = eventMsg.Data as HassStateChangedEventData;

            Assert.True(stateMessage?.EntityId == "binary_sensor.vardagsrum_pir");

            Assert.True(stateMessage.OldState?.EntityId == "binary_sensor.vardagsrum_pir");
            Assert.True(stateMessage.OldState?.Attributes != null &&
                        ((JsonElement)stateMessage.OldState?.Attributes?["battery_level"]).GetInt32()! == 100);
            Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["on"]).GetBoolean()!);
            Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["friendly_name"]).GetString()! ==
                        "Rörelsedetektor TV-rum");

            // Test the date and time conversions that it matches UTC time
            DateTime? lastChanged = stateMessage.OldState?.LastChanged;
            // Convert utc date to local so we can compare, this test will be ok on any timezone
            DateTime target = new DateTime(2019, 2, 17, 11, 41, 08, DateTimeKind.Utc).ToLocalTime();

            Assert.True(lastChanged.Value.Year == target.Year);
            Assert.True(lastChanged.Value.Month == target.Month);
            Assert.True(lastChanged.Value.Day == target.Day);
            Assert.True(lastChanged.Value.Hour == target.Hour);
            Assert.True(lastChanged.Value.Minute == target.Minute);
            Assert.True(lastChanged.Value.Second == target.Second);

            // Just test one of the NewStateOne
            Assert.True(stateMessage.NewState?.EntityId == "binary_sensor.vardagsrum_pir");
        }

        [Fact]
        public async Task SubscribeToEventsReturnsTrue()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            // ACT
            var subscribeTask = hassClient.SubscribeToEvents();
            // Add result success
            mock.AddResponse(@"{""id"": 2, ""type"": ""result"", ""success"": true, ""result"": null}");

            // ASSERT
            Assert.True(await subscribeTask);
        }

        [Fact]
        public async Task UnsupportedCommandMessageShouldBeLogged()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            hassClient.SendMessage(new UnknownCommand());
            //UnknownCommand
            mock.AddResponse(@"{""id"": 2, ""type"": ""result"", ""success"": true, ""result"": null}");

            await Task.Delay(20);

            mock.Logger.AssertLogged(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task ErrorCommandMessageShouldBeLogged()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            hassClient.SendMessage(new CallServiceCommand { Domain = "light", Service = "some_service" });
            mock.AddResponse(@"{""id"": 2, ""type"": ""result"", ""success"": false, ""result"": null, ""error"":{""code"": ""no_service"", ""message"": ""message""}}");

            await Task.Delay(20);

            mock.Logger.AssertLogged(LogLevel.Warning, Times.Once());
        }

        [Fact]
        public async Task ErrorCommandMessageCodeNonStringShouldBeLogged()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();

            hassClient.SendMessage(new CallServiceCommand { Domain = "light", Service = "some_service" });
            mock.AddResponse(@"{""id"": 2, ""type"": ""result"", ""success"": false, ""result"": null, ""error"":{""code"": 20, ""message"": ""message""}}");

            await Task.Delay(20);

            mock.Logger.AssertLogged(LogLevel.Warning, Times.Once());
        }

        [Fact]
        public async Task UnsupportedMessageReceivedShouldBeDebugLogged()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Don´t remove, the client does stuff in the background while delay
            // ReSharper disable once UnusedVariable
            var hassClient = await mock.GetHassConnectedClient();

            mock.AddResponse(@"{""type"": ""unknown""}");
            await Task.Delay(5);
            mock.Logger.AssertLogged(LogLevel.Debug, Times.AtLeast(1));
        }

        [Fact]
        public async Task WhenFactoryReturnsNullWebsocketReturnsFalseAndLogsError()
        {
            // ARRANGE
            var websocketFactoryMock = new Mock<IClientWebSocketFactory>();
            websocketFactoryMock.Setup(n => n.New()).Returns(() => null);

            var loggerMock = new LoggerMock();

            var hassClient =
                new JoySoftware.HomeAssistant.Client.HassClient(loggerMock.LoggerFactory, websocketFactoryMock.Object,
                    null);

            // ACT and ASSERT
            // Calls returns false and logs error
            Assert.False(await hassClient.ConnectAsync(new Uri("ws://anyurldoesntmatter.org"), "FAKETOKEN", false));
            loggerMock.AssertLogged(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task WrongMessagesFromHassShouldReturnFalse()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            // Get the default state hass client
            var hassClient = mock.GetHassClient();

            // First message from Home Assistant is auth required
            mock.AddResponse(@"{""type"": ""auth_required""}");
            // Next one we fake it is auth ok
            mock.AddResponse(@"{""type"": ""result""}");

            // ACT and ASSERT
            // Calls connect without getting the states initially
            Assert.False(await hassClient.ConnectAsync(new Uri("ws://anyurldoesntmatter.org"), "FAKETOKEN", false));
        }

        [Fact]
        public async Task HttpClientShouldCallCorrectHttpMessageHandler()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(
                    () => new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.OK, // Set non success return code
                        Content = new StringContent("{}", Encoding.UTF8)
                    }); ;

            // Get the default state hass client
            var hassClient = await mock.GetHassConnectedClient(false, httpMessageHandlerMock.Object);

            await hassClient.SetState("sensor.my_sensor", "new_state", new { attr1 = "hello" });

            // ACT and ASSERT
            // Calls connect without getting the states initially
            httpMessageHandlerMock.Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(1), // we expected a single external request
                    ItExpr.Is<HttpRequestMessage>(req =>
                            req.Method == HttpMethod.Post // we expected a GET request
                            && req.RequestUri ==
                            new Uri("http://anyurldoesntmatter.org/api/states/sensor.my_sensor") // to this uri
                    ),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        [Fact]
        public async Task SetStateNonSuccessHttpResponseCodeReturnNull()
        {

            // ARRANGE
            var mock = new HassWebSocketMock();
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(
                    () => new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.NotFound, // Set non success return code
                        Content = new StringContent("{}", Encoding.UTF8)
                    });

            // Get the default state hass client
            var hassClient = await mock.GetHassConnectedClient(false, httpMessageHandlerMock.Object);

            var result = await hassClient.SetState("sensor.my_sensor", "new_state", new { attr1 = "hello" });

            // ACT and ASSERT
            Assert.Null(result);

        }

        // [Fact]
        // public async Task SetStateTimeOutReturnNull()
        // {
        //     //Todo: Implement SetStateTimeOutReturnNull
        // }

        // [Fact]
        // public async Task SetStateExceptionLogsErrorAndReturnNull()
        // {
        //     //Todo: Implement SetStateExceptionLogsErrorAndReturnNull
        // }

        [Fact]
        public async Task SendEventHttpClientShouldCallCorrectHttpMessageHandler()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(
                    () => new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.OK, // Set non success return code
                        Content = new StringContent("{}", Encoding.UTF8)
                    }); ;

            // Get the default state hass client
            var hassClient = await mock.GetHassConnectedClient(false, httpMessageHandlerMock.Object);

            await hassClient.SendEvent("test_event", new { custom_data = "hello" });

            // ACT and ASSERT
            // Calls connect without getting the states initially
            httpMessageHandlerMock.Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(1), // we expected a single external request
                    ItExpr.Is<HttpRequestMessage>(req =>
                            req.Method == HttpMethod.Post // we expected a GET request
                            && req.RequestUri ==
                            new Uri("http://anyurldoesntmatter.org/api/events/test_event") // to this uri
                    ),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        [Fact]
        public async Task SendEventFaileHttpClientShouldReturnFalse()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(
                    () => new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.BadRequest, // Set non non success return code
                        Content = new StringContent("{}", Encoding.UTF8)
                    }); ;

            // Get the default state hass client
            var hassClient = await mock.GetHassConnectedClient(false, httpMessageHandlerMock.Object);

            var result = await hassClient.SendEvent("test_event", new { custom_data = "hello" });

            Assert.False(result);
        }

        [Fact]
        public async Task SendEventNoDataHttpClientShouldCallCorrectHttpMessageHandler()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(
                    () => new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.OK, // Set non success return code
                        Content = new StringContent("{}", Encoding.UTF8)
                    }); ;

            // Get the default state hass client
            var hassClient = await mock.GetHassConnectedClient(false, httpMessageHandlerMock.Object);

            await hassClient.SendEvent("test_event");

            // ACT and ASSERT
            // Calls connect without getting the states initially
            httpMessageHandlerMock.Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(1), // we expected a single external request
                    ItExpr.Is<HttpRequestMessage>(req =>
                            req.Method == HttpMethod.Post // we expected a GET request
                            && req.RequestUri ==
                            new Uri("http://anyurldoesntmatter.org/api/events/test_event") // to this uri
                    ),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        [Fact]
        public async Task ReadEventShouldCancel()
        {
            var mock = new HassWebSocketMock();
            // Get the connected hass client
            var hassClient = await mock.GetHassConnectedClient();
            var cancelSoon = new CancellationTokenSource(50);
            // ACT & ASSERT
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await hassClient.ReadEventAsync(cancelSoon.Token));
        }
    }
}