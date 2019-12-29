using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
namespace JoySoftware.HomeAssistant.Client.Unit.Tests
{
    public class HassClientTests
    {
        [Fact]
        public async void TestGoodConnect()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));

        }
        [Fact]
        public async void TestFailAuth()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthFail
            });

            var loggMock = new LoggerMock();

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(loggMock.LoggerFactory, mock);
            Assert.False(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));

            // Make sure we logged the error.
            loggMock.AssertLogged(LogLevel.Error, Times.AtLeastOnce());
        }

        [Fact]
        public async void TestNotExpectedMessageReturnFalse()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.ResultOk    // That message should not come here
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(wsFactory: mock);
            Assert.False(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));

        }

        [Fact]
        public async void TestConnectAllreadyConnectedFailure()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));

        }


        [Fact]
        public async void TestConnectNullUri()
        {
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthFail
            });
            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(wsFactory: mock);
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await hc.ConnectAsync(null, "lss", false, false));
        }

        [Fact]
        public async void TestConnectFail()
        {
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthFail
            });

            var loggMock = new LoggerMock();
            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(loggMock.LoggerFactory, mock);
            Assert.False(await hc.ConnectAsync(new Uri("ws://noconnect:9999"), "lss", false, false));

            loggMock.AssertLogged(LogLevel.Debug, Times.AtLeastOnce());
        }

        [Fact]
        public async void TestConnectWithSubscribeToEvents()
        {
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthOk,
                MockMessageType.ResultOk,
                MockMessageType.NewEvent
            });
            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, true));

            HassEvent eventMsg = await hc.ReadEventAsync();
            Assert.NotNull(eventMsg);

            var stateMessage = eventMsg?.Data as HassStateChangedEventData;

            Assert.True(stateMessage.EntityId == "binary_sensor.vardagsrum_pir");

            Assert.True(stateMessage.OldState?.EntityId == "binary_sensor.vardagsrum_pir");
            Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["battery_level"]).GetInt32() == 100);
            Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["on"]).GetBoolean() == true);
            Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["friendly_name"]).GetString() == "Rörelsedetektor TV-rum");

            // Test the date and time conversions that it matches UTC time
            DateTime? lastChanged = stateMessage?.OldState?.LastChanged;
            // Convert utc date to local so we can compare, this test will be ok on any timezone
            DateTime target = new DateTime(2019, 2, 17, 11, 41, 08, DateTimeKind.Utc).ToLocalTime();

            Assert.True(lastChanged?.Year == target.Year);
            Assert.True(lastChanged?.Month == target.Month);
            Assert.True(lastChanged?.Day == target.Day);
            Assert.True(lastChanged?.Hour == target.Hour);
            Assert.True(lastChanged?.Minute == target.Minute);
            Assert.True(lastChanged?.Second == target.Second);
        }

        [Fact]
        public async void TestClose()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));

            await hc.CloseAsync();
            Assert.True(mock.WebSocketClient.CloseIsRun);

        }

        [Fact]
        public async void TestConnectTimeout()
        {
            // Prepare the mock with no messages sent back so we trigger the timeout
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>());

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(wsFactory: mock)
            {
                SocketTimeout = 20 // set it to 20 ms timeout
            };
            Assert.False(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));

        }

        [Fact]
        public async void TestGetStates()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthOk,
                MockMessageType.States
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", true, false));
            Assert.True(hc.States.Count == 19);

        }

        [Fact]
        public async void TestGetConfig()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthOk
            });

            // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
            var hc = new HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));
            hc.SocketTimeout = 500000;

            System.Threading.Tasks.Task<HassConfig> confTask = hc.GetConfig();

            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.Config);

            HassConfig conf = confTask.Result;
            Assert.NotNull(conf);
            Assert.Equal("°C", conf?.UnitSystem?.Temperature);
            Assert.Contains<string>("binary_sensor.deconz", conf?.Components);
            Assert.Equal(62.2398549F, conf.Latitude);
            Assert.Contains<string>("/config/www", conf?.WhitelistExternalDirs);
            Assert.Equal("0.87.0", conf?.Version);

        }

        [Fact]
        public async void TestPingAndPong()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthOk,
            });

            var hc = new HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));

            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.Pong);
            Assert.True(await hc.PingAsync(1000));
        }

        [Fact]
        public async void TestCallServiceOk()
        {
            // Prepare the mock with predefined message sequence
            var mock = new HassWebSocketFactoryMock(new List<MockMessageType>()
            {
                MockMessageType.AuthRequired,
                MockMessageType.AuthOk,
            });

            // Just do normal connect
            var hc = new HassClient(wsFactory: mock);
            Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"), "TOKEN", false, false));

            // Add a fake service call message result 
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.ServiceCallOk);

            Assert.True(await hc.CallService("light", "turn_on", new { entity_id = "light.tomas_rum" }));

            System.Threading.Tasks.Task<HassEvent> eventTask = hc.ReadEventAsync();
            mock.WebSocketClient.ResponseMessages.Writer.TryWrite(MockMessageType.ServiceEvent);

            var serviceEvent = eventTask.Result?.Data as HassServiceEventData;
            JsonElement? c = serviceEvent?.ServiceData?.GetProperty("entity_id");
            Assert.Equal("light.tomas_rum", c.Value.GetString());
            Assert.NotNull(serviceEvent);
        }

    }
}
