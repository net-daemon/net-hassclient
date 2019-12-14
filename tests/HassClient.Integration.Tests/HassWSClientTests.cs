using System;
using System.Threading.Tasks;
using HassClient;
using System.Text.Json;
using HassClient.Performance.Tests.Mocks;
using Xunit;
using Xunit.Abstractions;

namespace HassClient.Integration.Tests
{
    public class TestWSClient : IClassFixture<HomeAssistantMockFixture>
    {
        HomeAssistantMockFixture mockFixture;
        private readonly ITestOutputHelper output;
        public TestWSClient(HomeAssistantMockFixture fixture, ITestOutputHelper output)
        {
            mockFixture = fixture;
            this.output = output;
        }

        [Fact]
        public async void TestBasicLoginOK()
        {
            using WSClient wscli = new WSClient();
            var result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            var message = await wscli.ReadMessageAsync();

            Assert.True(message.Type == "auth_required");

            wscli.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            message = await wscli.ReadMessageAsync();

            Assert.True(message.Type == "auth_ok");

            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestBasicLoginNotOK()
        {
            using WSClient wscli = new WSClient();
            var result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            var message = await wscli.ReadMessageAsync();

            Assert.True(message.Type == "auth_required");
            wscli.SendMessage(new AuthMessage { AccessToken = "WRONG PASSWORD" });
            message = await wscli.ReadMessageAsync();
            Assert.True(message.Type == "auth_invalid");

            await wscli.CloseAsync();

        }
        [Fact]
        public async void TestGetStatesMessage()
        {
            using WSClient wscli = new WSClient();
            var result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            // Just read the auth_required message
            await wscli.ReadMessageAsync();

            // Send the get states message
            wscli.SendMessage(new GetStatesMessage { });

            // Read response result
            var message = await wscli.ReadMessageAsync();

            // Assert.True(true);
            await wscli.CloseAsync();
        }

        [Fact]
        public async void TestListenEvent()
        {
            using WSClient wscli = new WSClient();
            var result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            // Just read the auth_required message
            await wscli.ReadMessageAsync();

            // Send the subscribe message
            wscli.SendMessage(new SubscribeEventMessage { });

            // Read response result
            var message = await wscli.ReadMessageAsync();
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
            var lastChanged = stateMessage?.OldState?.LastChanged;
            var target = new DateTime(2019, 2, 17, 11, 41, 08, DateTimeKind.Utc);

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
            using WSClient wscli = new WSClient();
            var result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            // Just read the auth_required message
            await wscli.ReadMessageAsync();
            await wscli.CloseAsync();

            await Task.Delay(1000);
            result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            // Just read the auth_required message
            var message = await wscli.ReadMessageAsync();
            Assert.True(message.Type == "auth_required");
            await wscli.CloseAsync();
        }

    }
    public class HomeAssistantMockFixture : IDisposable
    {
        HomeAssistantMock mock;
        public HomeAssistantMockFixture()
        {
            mock = new HomeAssistantMock();
        }
        public void Dispose()
        {
            mock.Stop();
        }
    }
}