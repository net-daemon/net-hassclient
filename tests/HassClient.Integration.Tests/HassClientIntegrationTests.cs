using HassClient.Performance.Tests.Mocks;
using System;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace HassClient.Integration.Tests
{
    public class TestWSClient : IDisposable
    {
        private readonly HomeAssistantMock mock;
        private readonly ITestOutputHelper output;
        public TestWSClient(ITestOutputHelper output)
        {
            mock = new HomeAssistantMock();
            this.output = output;
        }


        public void Dispose() => mock.Stop();


        [Fact]
        public async void TestBasicLoginOK()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false, false);
            Assert.True(result);
            Assert.True(wscli.States.Count == 0);

            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestBasicLoginFail()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "WRONG PASSWORD", false, false);
            Assert.False(result);
            Assert.True(wscli.States.Count == 0);

            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestFetchStates()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", true, false);
            Assert.True(result);
            Assert.True(wscli.States.Count == 19);
            Assert.True(wscli.States["binary_sensor.vardagsrum_pir"].State == "on");
            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestSubscribeEvents()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false, true);
            Assert.True(result);

            var eventMsg = await wscli.ReadEventAsync();

            var stateMessage = eventMsg?.Data as StateChangedEventMessage;

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

            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestNormalConnectAllOptionsTrue()
        {
            using var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", true, true);
            Assert.True(result);
            Assert.True(wscli.States.Count == 19);
            Assert.True(wscli.States["binary_sensor.vardagsrum_pir"].State == "on");
            var eventMsg = await wscli.ReadEventAsync();

            Assert.Equal("state_changed", eventMsg.EventType);
        }

        [Fact]
        public async void TestServerFailedConnect()
        {
            var loggerFactoryMock = new LoggerFactoryMock();
            using var wscli = new HassClient(loggerFactoryMock);
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket_not_exist"), "ABCDEFGHIJKLMNOPQ");
            Assert.False(result);
            Assert.True(loggerFactoryMock.LoggedError);
            Assert.True(loggerFactoryMock.LoggedDebug);
            Assert.False(loggerFactoryMock.LoggedTrace);
            await wscli.CloseAsync();

        }

        //[Fact]
        //public async void TestServerDisconnect()
        //{
        //    using var wscli = new HassClient();
        //    bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
        //    HassMessage message = await wscli.ReadMessageAsync();


        //    wscli.SendMessage(new MessageBase { Type = "fake_disconnect_test" });
        //    await Assert.ThrowsAsync<OperationCanceledException>(async () => await wscli.ReadMessageAsync());
        //    await wscli.CloseAsync();
        //}

        //[Fact]
        //public async void TestListenEvent()
        //{
        //    using var wscli = new HassClient();
        //    bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
        //    // Just read the auth_required message
        //    await wscli.ReadMessageAsync();

        //    // Send the subscribe message
        //    wscli.SendMessage(new SubscribeEventMessage { });

        //    // Read response result
        //    HassMessage message = await wscli.ReadMessageAsync();
        //    Assert.True(message.Type == "result");
        //    Assert.True(message.Success == true);
        //    Assert.True(message.Id == 1);

        //    // Read the new event message (this will only happen in mock server)
        //    // This is the event that is in /Mocks/testada/event.json
        //    var evMessage = await wscli.ReadEventAsync();
        //    var stateMessage = evMessage?.Data as StateChangedEventMessage;

        //    Assert.True(stateMessage.EntityId == "binary_sensor.vardagsrum_pir");

        //    Assert.True(stateMessage.OldState?.EntityId == "binary_sensor.vardagsrum_pir");
        //    Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["battery_level"]).GetInt32() == 100);
        //    Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["on"]).GetBoolean() == true);
        //    Assert.True(((JsonElement)stateMessage.OldState?.Attributes?["friendly_name"]).GetString() == "Rörelsedetektor TV-rum");

        //    // Test the date and time conversions that it matches UTC time
        //    DateTime? lastChanged = stateMessage?.OldState?.LastChanged;
        //    // Convert utc date to local so we can compare, this test will be ok on any timezone
        //    DateTime target = new DateTime(2019, 2, 17, 11, 41, 08, DateTimeKind.Utc).ToLocalTime();

        //    Assert.True(lastChanged?.Year == target.Year);
        //    Assert.True(lastChanged?.Month == target.Month);
        //    Assert.True(lastChanged?.Day == target.Day);
        //    Assert.True(lastChanged?.Hour == target.Hour);
        //    Assert.True(lastChanged?.Minute == target.Minute);
        //    Assert.True(lastChanged?.Second == target.Second);

        //    wscli.SendMessage(new SubscribeEventMessage { });
        //    message = await wscli.ReadMessageAsync();
        //    Assert.True(message.Id == 2);

        //    await wscli.CloseAsync();

        //}

        //[Fact]
        //public async void TestReconnectFromNormalDisconnect()
        //{
        //    using var wscli = new HassClient();
        //    bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
        //    // Just read the auth_required message
        //    await wscli.ReadMessageAsync();
        //    await wscli.CloseAsync();

        //    await Task.Delay(1000);
        //    result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
        //    // Just read the auth_required message
        //    HassMessage message = await wscli.ReadMessageAsync();
        //    Assert.True(message.Type == "auth_required");
        //    await wscli.CloseAsync();
        //}

    }
}