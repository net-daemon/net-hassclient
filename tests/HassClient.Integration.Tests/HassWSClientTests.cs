using HassClient.Performance.Tests.Mocks;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
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
            using var wscli = new WSClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            HassMessage message = await wscli.ReadMessageAsync();

            Assert.True(message.Type == "auth_required");

            wscli.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            message = await wscli.ReadMessageAsync();

            Assert.True(message.Type == "auth_ok");

            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestBasicLoginNotOK()
        {
            using var wscli = new WSClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            HassMessage message = await wscli.ReadMessageAsync();

            Assert.True(message.Type == "auth_required");
            wscli.SendMessage(new AuthMessage { AccessToken = "WRONG PASSWORD" });
            message = await wscli.ReadMessageAsync();
            Assert.True(message.Type == "auth_invalid");

            await wscli.CloseAsync();

        }

        [Fact]
        public async void TestServerFailedConnect()
        {
            var loggerFactoryMock = new LoggerFactoryMock();
            using var wscli = new WSClient(loggerFactoryMock);
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket_not_exist"));
            Assert.False(result);
            Assert.True(loggerFactoryMock.LoggedError);
            Assert.True(loggerFactoryMock.LoggedDebug);
            Assert.False(loggerFactoryMock.LoggedTrace);
            await wscli.CloseAsync();

        }

        [Fact]
        public async void TestServerDisconnect()
        {
            using var wscli = new WSClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            HassMessage message = await wscli.ReadMessageAsync();


            wscli.SendMessage(new MessageBase { Type = "fake_disconnect_test" });
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await wscli.ReadMessageAsync());
            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestGetStatesMessage()
        {
            using var wscli = new WSClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            // Just read the auth_required message
            await wscli.ReadMessageAsync();

            // Send the get states message
            wscli.SendMessage(new GetStatesMessage { });

            // Read response result, see result_states.json file for this result
            HassMessage message = await wscli.ReadMessageAsync();
            var wsResult = message?.Result as List<StateMessage>;

            Assert.True(wsResult?[8].EntityId == "binary_sensor.vardagsrum_pir");
            Assert.True(wsResult?[8].State == "on");
            Assert.True(((JsonElement)wsResult?[8].Attributes?["on"]).GetBoolean() == true);

            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestListenEvent()
        {
            using var wscli = new WSClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            // Just read the auth_required message
            await wscli.ReadMessageAsync();

            // Send the subscribe message
            wscli.SendMessage(new SubscribeEventMessage { });

            // Read response result
            HassMessage message = await wscli.ReadMessageAsync();
            Assert.True(message.Type == "result");
            Assert.True(message.Success == true);
            Assert.True(message.Id == 1);

            // Read the new event message (this will only happen in mock server)
            // This is the event that is in /Mocks/testada/event.json
            message = await wscli.ReadMessageAsync();
            var stateMessage = message?.Event?.Data as StateChangedEventMessage;

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

            wscli.SendMessage(new SubscribeEventMessage { });
            message = await wscli.ReadMessageAsync();
            Assert.True(message.Id == 2);

            await wscli.CloseAsync();

        }

        [Fact]
        public async void TestReconnectFromNormalDisconnect()
        {
            using var wscli = new WSClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            // Just read the auth_required message
            await wscli.ReadMessageAsync();
            await wscli.CloseAsync();

            await Task.Delay(1000);
            result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            // Just read the auth_required message
            HassMessage message = await wscli.ReadMessageAsync();
            Assert.True(message.Type == "auth_required");
            await wscli.CloseAsync();
        }

    }
}