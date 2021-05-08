using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HassClientIntegrationTests.Mocks;
using JoySoftware.HomeAssistant.Client;
using JoySoftware.HomeAssistant.Extensions;
using JoySoftware.HomeAssistant.Messages;
using JoySoftware.HomeAssistant.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            await using var wscli = CreateHassClient();
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
            await using var wscli = CreateHassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "WRONG PASSWORD",
                false);
            Assert.False(result);
            Assert.True(wscli.States.IsEmpty);

            await wscli.CloseAsync();
        }


        [Fact]
        public async Task TestBasicLoginOK()
        {
            await using var wscli = CreateHassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);
            Assert.True(result);
            Assert.True(wscli.States.IsEmpty);

            await wscli.CloseAsync();
        }

        [Fact]
        public async Task TestClose()
        {
            await using var wscli = CreateHassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);
            Assert.True(result);
            Assert.True(wscli.States.IsEmpty);
            // Wait for event that never comes
            var eventTask = wscli.ReadEventAsync();
            // Do close
            await wscli.CloseAsync();
            Assert.Throws<AggregateException>(() => eventTask.Result);
        }

        [Fact]
        public async Task TestFetchStates()
        {
            await using var wscli = CreateHassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
            Assert.True(result);
            Assert.True(wscli.States.Count == 19);
            Assert.True(wscli.States["binary_sensor.vardagsrum_pir"].State == "on");
            await wscli.CloseAsync();
        }

        [Fact]
        public async Task TestGetAreas()
        {
            await using var wscli = CreateHassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false);


            // ACT
            // HassEvent eventMsg = await hassClient.ReadEventAsync();
            var areas = await wscli.GetAreas().ConfigureAwait(false);
            var first = areas.FirstOrDefault();

            // ASSERT
            Assert.NotNull(areas);
            Assert.NotNull(first);
            Assert.Equal("Bedroom", first.Name);
            Assert.Equal("5a30cdc2fd7f44d5a77f2d6f6d2ccd76", first.Id);

            Assert.Equal(3, areas.Count);
        }

        [Fact]
        public async Task TestGetDevices()
        {
            // ARRANGE
            await using var wscli = CreateHassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false);


            // ACT
            var devices = await wscli.GetDevices().ConfigureAwait(false);
            var first = devices.FirstOrDefault();

            // ASSERT
            Assert.NotNull(devices);
            Assert.NotNull(first);
            Assert.Null(first.NameByUser);
            Assert.Null(first.AreaId);
            Assert.Equal("Google Inc.", first.Manufacturer);
            Assert.Equal("42cdda32a2a3428e86c2e27699d79ead", first.Id);
            Assert.Equal("Chromecast", first.Model);
            Assert.Equal("My TV", first.Name);

            Assert.Equal(2, devices.Count);
        }
        [Fact]
        public async Task TestGetEntities()
        {
            // ARRANGE
            await using var wscli = CreateHassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false);

            // ACT

            var entities = await wscli.GetEntities().ConfigureAwait(false);
            var first = entities.FirstOrDefault();

            // ASSERT
            Assert.NotNull(entities);
            Assert.NotNull(first);
            Assert.Null(first.Name);
            Assert.Null(first.Icon);
            Assert.Equal("42cdda32a2a3428e86c2e27699d79ead", first.DeviceId);
            Assert.Equal("media_player.tv_uppe2", first.EntityId);

            Assert.Equal(2, entities.Count);
        }

        [Fact]
        public async Task TestPingPong()
        {
            await using var wscli = CreateHassClient();
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
            await using var wscli = CreateHassClient(loggerFactoryMock);
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket_not_exist"),
                "ABCDEFGHIJKLMNOPQ");
            Assert.False(result);
            Assert.False(loggerFactoryMock.LoggedError);
            Assert.True(loggerFactoryMock.LoggedDebug);
            Assert.False(loggerFactoryMock.LoggedTrace);
            await wscli.CloseAsync();
        }

        [Fact]
        public async Task TestSubscribeEvents()
        {
            await using var wscli = CreateHassClient();
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

        private HassClient CreateHassClient(ILoggerFactory factory = null)
        {
            var services = new ServiceCollection();

            if (factory is not null)
            {
                services.AddTransient<ILoggerFactory>(_ => factory);
            }
            
            services.AddHassClient();

            var provider = services.BuildServiceProvider();
            // TODO: Get rid of cast. Tests shouldn't rely on internal behaviour.
            return (HassClient) provider.GetRequiredService<IHassClient>();
        }
    }
}