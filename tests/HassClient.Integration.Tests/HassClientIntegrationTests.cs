using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HassClientIntegrationTests.Mocks;
using JoySoftware.HomeAssistant.Client;
using Xunit;

namespace HassClientIntegrationTests
{
    public class TestWSClient : IAsyncLifetime
    {
        // public TestWSClient()
        // {

        // }
        public Task InitializeAsync()
        {
            _mock = new HomeAssistantMock();
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _mock.DisposeAsync().ConfigureAwait(false);
        }
        private HomeAssistantMock _mock;

        [Fact]
        public async Task RemoteCloseThrowsException()
        {
            await using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false).ConfigureAwait(false);

            // Skip first authorize message
            var eventTask = wscli.ReadEventAsync();
            await wscli.SendMessage(new CommandMessage { Id = 2, Type = "fake_disconnect_test" }).ConfigureAwait(false);
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await eventTask.ConfigureAwait(false)).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestBasicLoginFail()
        {
            await using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "WRONG PASSWORD",
                false);
            Assert.False(result);
            Assert.True(wscli.States.Count == 0);

            await wscli.CloseAsync();
        }


        [Fact]
        public async Task TestBasicLoginOK()
        {
            await using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);
            Assert.True(result);
            Assert.True(wscli.States.Count == 0);

            await wscli.CloseAsync();
        }

        [Fact]
        public async Task TestClose()
        {
            await using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);
            Assert.True(result);
            Assert.True(wscli.States.Count == 0);
            // Wait for event that never comes
            var eventTask = wscli.ReadEventAsync();
            // Do close
            await wscli.CloseAsync();
            Assert.Throws<AggregateException>(() => eventTask.Result);
        }

        [Fact]
        public async Task TestFetchStates()
        {
            await using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
            Assert.True(result);
            Assert.True(wscli.States.Count == 19);
            Assert.True(wscli.States["binary_sensor.vardagsrum_pir"].State == "on");
            await wscli.CloseAsync();
        }

        [Fact]
        public async Task TestPingPong()
        {
            await using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);
            Assert.True(result);

            bool pongReceived = await wscli.PingAsync(1000);
            Assert.True(pongReceived);
            await wscli.CloseAsync();
        }

        [Fact]
        public async Task TestServerFailedConnect()
        {
            var loggerFactoryMock = new LoggerFactoryMock();
            await using var wscli = new HassClient(loggerFactoryMock);
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket_not_exist"),
                "ABCDEFGHIJKLMNOPQ");
            Assert.False(result);
            Assert.True(loggerFactoryMock.LoggedError);
            Assert.True(loggerFactoryMock.LoggedDebug);
            Assert.False(loggerFactoryMock.LoggedTrace);
            await wscli.CloseAsync();
        }

        [Fact]
        public async Task TestSubscribeEvents()
        {
            await using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);
            Assert.True(result);

            Assert.True(await wscli.SubscribeToEvents());
            HassEvent eventMsg = await wscli.ReadEventAsync();

            var stateMessage = eventMsg?.Data as HassStateChangedEventData;

            Assert.True(stateMessage != null && stateMessage.EntityId == "binary_sensor.vardagsrum_pir");

            Assert.True(stateMessage.OldState?.EntityId == "binary_sensor.vardagsrum_pir");
            Assert.True(stateMessage.OldState?.Attributes != null &&
                        ((JsonElement)stateMessage.OldState?.Attributes?["battery_level"]).GetInt32()! == 100);
            Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["on"]).GetBoolean()!);
            Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["friendly_name"]).GetString()! ==
                        "Rörelsedetektor TV-rum");

            // Test the date and time conversions that it matches UTC time
            DateTime? lastChanged = stateMessage.OldState?.LastChanged;
            // Convert utc date to local so we can compare, this test will be ok on any timezone
            DateTime targetChanged = new DateTime(2019, 2, 17, 11, 41, 08, DateTimeKind.Utc).ToLocalTime();

            // Test the date and time conversions that it matches UTC time
            DateTime? lastUpdated = stateMessage.OldState?.LastUpdated;
            // Convert utc date to local so we can compare, this test will be ok on any timezone
            DateTime targetUpdated = new DateTime(2019, 2, 17, 11, 42, 08, DateTimeKind.Utc).ToLocalTime();

            Assert.True(lastChanged.Value.Year == targetChanged.Year);
            Assert.True(lastChanged.Value.Month == targetChanged.Month);
            Assert.True(lastChanged.Value.Day == targetChanged.Day);
            Assert.True(lastChanged.Value.Hour == targetChanged.Hour);
            Assert.True(lastChanged.Value.Minute == targetChanged.Minute);
            Assert.True(lastChanged.Value.Second == targetChanged.Second);

            Assert.True(lastUpdated.Value.Year == targetUpdated.Year);
            Assert.True(lastUpdated.Value.Month == targetUpdated.Month);
            Assert.True(lastUpdated.Value.Day == targetUpdated.Day);
            Assert.True(lastUpdated.Value.Hour == targetUpdated.Hour);
            Assert.True(lastUpdated.Value.Minute == targetUpdated.Minute);
            Assert.True(lastUpdated.Value.Second == targetUpdated.Second);
            await wscli.CloseAsync();
        }

        [Fact]
        public async Task ProcessMessageGetCorrectResult()
        {
            //ARRANGE
            var wsFactory = new ClientWebSocketFactory();
            var ws = wsFactory.New();

            // DisposeAsync will close websocket
            await ws.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), CancellationToken.None).ConfigureAwait(false);

            // ACT
            await using var pipe = WebSocketMessagePipeline<HassMessageBase>.CreateWebSocketMessagePipeline(ws);
            var x = await pipe.GetNextMessageAsync(CancellationToken.None).ConfigureAwait(false);

            // await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None).ConfigureAwait(false);

            // ASSERT
            Assert.Equal("auth_required", x.Type);
        }

        [Fact]
        public async Task ProcessBigMessageGetCorrectResult()
        {
            //ARRANGE
            var wsFactory = new ClientWebSocketFactory();
            var ws = wsFactory.New();
            var cancelSource = new CancellationTokenSource();

            // DisposeAsync will close websocket
            await ws.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), CancellationToken.None).ConfigureAwait(false);

            // ACT
            await using var pipe = WebSocketMessagePipeline<HassMessageBase>.CreateWebSocketMessagePipeline(ws);
            _ = await pipe.GetNextMessageAsync(CancellationToken.None).ConfigureAwait(false);


            await pipe.SendMessageAsync(new HassMessageBase { Type = "get_states" }, cancelSource.Token);

            var msg = await pipe.GetNextMessageAsync(CancellationToken.None).ConfigureAwait(false);

            // ASSERT
            Assert.Equal("result", msg.Type);

        }

        [Fact]
        public async Task ProcessMessageReturnWhenClosed()
        {
            //ARRANGE
            var wsFactory = new ClientWebSocketFactory();
            var ws = wsFactory.New();
            var cancelSource = new CancellationTokenSource();

            // DisposeAsync will close websocket
            await ws.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), CancellationToken.None).ConfigureAwait(false);

            // ACT
            await using var pipe = WebSocketMessagePipeline<HassMessageBase>.CreateWebSocketMessagePipeline(ws);

            // First read the first sent
            var msg = await pipe.GetNextMessageAsync(cancelSource.Token).ConfigureAwait(false);
            Assert.Equal("auth_required", msg.Type);

            var task = pipe.GetNextMessageAsync(cancelSource.Token).ConfigureAwait(false);

            await pipe.CloseAsync();

            var x = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);

            Assert.True(x is object);
            Assert.False(pipe.IsValid);

        }

        [Fact]
        public async Task ProcessMessageReturnWhenRemoteClosed()
        {
            //ARRANGE
            var wsFactory = new ClientWebSocketFactory();
            var ws = wsFactory.New();
            var cancelSource = new CancellationTokenSource();

            // DisposeAsync will close websocket
            await ws.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), CancellationToken.None).ConfigureAwait(false);

            // ACT
            await using var pipe = WebSocketMessagePipeline<HassMessageBase>.CreateWebSocketMessagePipeline(ws);

            // First read the first sent
            var msg = await pipe.GetNextMessageAsync(cancelSource.Token).ConfigureAwait(false);
            Assert.Equal("auth_required", msg.Type);


            var task = pipe.GetNextMessageAsync(cancelSource.Token).ConfigureAwait(false);

            await Task.Delay(100);

            await pipe.SendMessageAsync(new HassMessageBase { Type = "fake_disconnect_test" }, cancelSource.Token);

            var x = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);

            Assert.True(x is object);
            Assert.False(pipe.IsValid);

        }


    }
}