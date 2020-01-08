using HassClientIntegrationTests.Mocks;
using JoySoftware.HomeAssistant.Client;
using System;
using System.Text.Json;
using Xunit;

namespace HassClientIntegrationTests
{
    public class TestWSClient : IDisposable
    {
        public TestWSClient()
        {
            _mock = new HomeAssistantMock();
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        private readonly HomeAssistantMock _mock;
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _mock.Stop();
                    _mock.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        [Fact]
        public async void TestBasicLoginFail()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "WRONG PASSWORD",
                false);
            Assert.False(result);
            Assert.True(wscli.States.Count == 0);

            await wscli.CloseAsync();
        }


        [Fact]
        public async void TestBasicLoginOK()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);
            Assert.True(result);
            Assert.True(wscli.States.Count == 0);

            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestClose()
        {
            using var wscli = new HassClient();
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
        public async void RemoteCloseThrowsException()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);
            var eventTask = wscli.ReadEventAsync();
            wscli.SendMessage(new CommandMessage() { Id = 2, Type = "fake_disconnect_test" });

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await eventTask);
        }

        [Fact]
        public async void TestFetchStates()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
            Assert.True(result);
            Assert.True(wscli.States.Count == 19);
            Assert.True(wscli.States["binary_sensor.vardagsrum_pir"].State == "on");
            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestPingPong()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);
            Assert.True(result);

            bool pongReceived = await wscli.PingAsync(1000);
            Assert.True(pongReceived);
            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestServerFailedConnect()
        {
            var loggerFactoryMock = new LoggerFactoryMock();
            using var wscli = new HassClient(loggerFactoryMock);
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket_not_exist"),
                "ABCDEFGHIJKLMNOPQ");
            Assert.False(result);
            Assert.True(loggerFactoryMock.LoggedError);
            Assert.True(loggerFactoryMock.LoggedDebug);
            Assert.False(loggerFactoryMock.LoggedTrace);
            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestSubscribeEvents()
        {
            using var wscli = new HassClient();
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
    }
}