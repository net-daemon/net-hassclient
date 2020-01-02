using JoySoftware.HomeAssistant.Client;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        public async void TestSubscribeToAllEventTypes(EventType eventType)
        {
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });
            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));

            var subscribeTask = hc.SubscribeToEvents(eventType);
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.ResultOk);
            Assert.True(subscribeTask.Result);
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.NewEvent);
            HassEvent eventMsg = await hc.ReadEventAsync();
            Assert.NotNull(eventMsg);
        }

        [Fact]
        public async void TestCallServiceCanceled()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Just do normal connect
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));

            // Do not add a fake service call message result 
            //mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.ServiceCallOk);

            var callServiceTask = hc.CallService("light", "turn_on", new { entity_id = "light.tomas_rum" });
            hc.CancelSource.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await callServiceTask);
        }

        [Fact]
        public async void TestCallServiceGeneralException()
        {
            var webSocketMock = Mock.Of<IClientWebSocket>(ws =>
                ws.SendAsync(null, WebSocketMessageType.Text, true, CancellationToken.None) ==
                Task.FromException(new Exception("Some exception")));

            var factoryMock = Mock.Of<IClientWebSocketFactory>(mf =>
                mf.New() == webSocketMock
            );

            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: factoryMock);

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await hc.SubscribeToEvents());
        }

        [Fact]
        public async void TestCallServiceOk()
        {
            // Arrange

            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });
            // Just do normal connect
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false);
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.ServiceCallOk);

            // Act
            var result = await hc.CallService("light", "turn_on", new { entity_id = "light.tomas_rum" });

            // Assert 
            Assert.True(result);

            // Act on getting the event data (should be another test?)
            Task<HassEvent> eventTask = hc.ReadEventAsync();
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.ServiceEvent);

            var serviceEvent = eventTask.Result?.Data as HassServiceEventData;
            JsonElement? c = serviceEvent?.ServiceData?.GetProperty("entity_id");
            // Assert
            Assert.NotNull(serviceEvent);
            Assert.Equal("light", serviceEvent?.Domain);
            Assert.Equal("toggle", serviceEvent?.Service!);
            Assert.Equal("light.tomas_rum", c?.GetString());
        }

        [Fact]
        public async void TestCallServiceTimeout()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Just do normal connect
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));

            hc.SocketTimeout = 10;
            // Do not add a message and force timeout
            Assert.False(await hc.CallService("light", "turn_on", new { entity_id = "light.tomas_rum" }));
        }

        [Fact]
        public async Task TestClose()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            var result = await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false);

            Assert.True(result);

            await hc.CloseAsync();
            Assert.True(mock.WebSocketClient.CloseIsRun);
        }

        //TODO: Fix the test
        [Fact]
        public async void TestCloseWithTimeout()
        {
            var messages = new Queue<byte[]>();
            messages.Enqueue(Encoding.ASCII.GetBytes(@"{""type"": ""auth_required""}"));
            messages.Enqueue(Encoding.ASCII.GetBytes(@"{""type"": ""auth_ok""}"));

            var loggerMock = new LoggerMock();

            var webSocketMock = new Mock<IClientWebSocket>();

            webSocketMock.Setup(x => x.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(2));
            webSocketMock.Setup(x =>
                    x.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromException<OperationCanceledException>(new OperationCanceledException("Fake close")));

            webSocketMock.Setup(x => x.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask());

            webSocketMock.Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    (Memory<byte> buffer, CancellationToken token) =>
                    {
                        if (messages.Count == 0)
                        {
                            Task.Delay(10000, token).Wait(token); // Will be canceled so a high value
                            return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                        }

                        var nextMsg = messages.Dequeue();
                        nextMsg.CopyTo(buffer);
                        var webResult =
                            new ValueWebSocketReceiveResult(nextMsg.Length, WebSocketMessageType.Text, true);

                        return webResult;
                    });

            webSocketMock.SetupGet(x => x.State).Returns(WebSocketState.Open);

            var factoryMock = Mock.Of<IClientWebSocketFactory>(mf =>
                mf.New() == webSocketMock.Object
            );

            var hc = new JoySoftware.HomeAssistant.Client.HassClient(loggerMock.LoggerFactory,
                factoryMock); // { SocketTimeout = 20 };

            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8123"), "TOKEN", false));
            hc.SocketTimeout = 20;
            await hc.CloseAsync();
            loggerMock.AssertLogged(LogLevel.Trace, Times.AtLeastOnce());
        }

        [Fact]
        public async void TestCommandWithNotExpectedResultBack()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));
            hc.SocketTimeout = 500000;

            Task<HassConfig> confTask = hc.GetConfig();

            // First add an unexpected message
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.ResultNotExpected);
            // Then add the expected one... It should recover from that...
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.Config);

            HassConfig conf = confTask.Result;

            Assert.NotNull(conf);
        }

        [Fact]
        public async void TestCommandWithSuccessFalse()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));
            hc.SocketTimeout = 500000;

            Task<HassConfig> confTask = hc.GetConfig();

            // First add an unexpected message
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.ResultNotOk);

            Assert.Throws<AggregateException>(() => confTask.Result);
        }

        [Fact]
        public async void TestConnectAlreadyConnectedFailure()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));
        }

        [Fact]
        public async void TestConnectAndSubscribeToEvents()
        {
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });
            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));

            var subscribeTask = hc.SubscribeToEvents();
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.ResultOk);
            Assert.True(subscribeTask.Result);
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.NewEvent);
            HassEvent eventMsg = await hc.ReadEventAsync();
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
        }

        [Fact]
        public async void TestConnectFail()
        {
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthFail
            });

            var loggMock = new LoggerMock();
            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(loggMock.LoggerFactory, mock);
            Assert.False(await hc.ConnectAsync(new Uri("ws://noconnect:9999"), "lss", false));

            loggMock.AssertLogged(LogLevel.Debug, Times.AtLeastOnce());
        }


        [Fact]
        public async void TestConnectNullUri()
        {
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthFail
            });
            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await hc.ConnectAsync(null, "lss", false));
        }

        [Fact]
        public async void TestConnectTimeout()
        {
            // Prepare the mock with no messages sent back so we trigger the timeout
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>());

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock)
            {
                SocketTimeout = 20 // set it to 20 ms timeout
            };
            Assert.False(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));
        }

        [Fact]
        public async void TestFailAuth()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthFail
            });

            var loggMock = new LoggerMock();

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(loggMock.LoggerFactory, mock);
            Assert.False(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));

            // Make sure we logged the error.
            loggMock.AssertLogged(LogLevel.Error, Times.AtLeastOnce());
        }

        [Fact]
        public async void TestGetConfig()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));

            Task<HassConfig> confTask = hc.GetConfig();

            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.Config);

            HassConfig conf = confTask.Result;
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
        public async void TestGetStates()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            var hcTask = hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN");


            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.States);
            Assert.True(await hcTask);
            Assert.Equal(19, hc.States.Count);
        }

        [Fact]
        public async void TestGoodConnect()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));
        }

        [Fact]
        public async void TestGoodConnectWithHostAndPort()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync("localhost", 8192, false, "TOKEN", false));
        }

        [Fact]
        public async void TestNotExpectedMessageReturnFalse()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.ResultOk // That message should not come here
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.False(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));
        }

        [Fact]
        public async void TestPingAndPong()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));

            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.Pong);
            Assert.True(await hc.PingAsync(1000));
        }

        [Fact]
        public async void TestPingAndPongFail()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>
            {
                MockMessageType.AuthRequired, MockMessageType.AuthOk
            });

            var hc = new JoySoftware.HomeAssistant.Client.HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false));

            // Do not write pong message back and test
            Assert.False(await hc.PingAsync(2));
        }
    }
}