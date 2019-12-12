using System;
using System.Threading.Tasks;
using HassClient;
using HassClient.Performance.Tests.Mocks;
using Xunit;
using Xunit.Abstractions;

namespace HassClient.Integration.Tests {
    public class UnitTest1 : IClassFixture<HomeAssistantMockFixture> {
        HomeAssistantMockFixture mockFixture;
        private readonly ITestOutputHelper output;
        public UnitTest1 (HomeAssistantMockFixture fixture, ITestOutputHelper output) {
            mockFixture = fixture;
            this.output = output;
        }

        [Fact]
        public async void TestBasicLoginOK () {
            WSClient wscli = new WSClient ();
            var result = await wscli.ConnectAsync (new Uri ("ws://127.0.0.1:5001/api/websocket"));
            var message = await wscli.ReadMessageAsync ();

            Assert.True (message.Type == "auth_required");

            wscli.SendMessage (new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            message = await wscli.ReadMessageAsync ();

            Assert.True (message.Type == "auth_ok");

            await wscli.DisconnectAsync ();
        }

        [Fact]
        public async void TestBasicLoginNotOK () {
            WSClient wscli = new WSClient ();
            var result = await wscli.ConnectAsync (new Uri ("ws://127.0.0.1:5001/api/websocket"));
            var message = await wscli.ReadMessageAsync ();

            Assert.True (message.Type == "auth_required");
            wscli.SendMessage (new AuthMessage { AccessToken = "WRONG PASSWORD" });
            message = await wscli.ReadMessageAsync ();
            Assert.True (message.Type == "auth_invalid");

            await wscli.DisconnectAsync ();

        }

        [Fact]
        public async void TestListenEvent () {
            WSClient wscli = new WSClient ();
            var result = await wscli.ConnectAsync (new Uri ("ws://127.0.0.1:5001/api/websocket"));
            // Just read the auth_required message
            await wscli.ReadMessageAsync ();

            wscli.SendMessage (new SubscribeEventMessage { });

            var message = await wscli.ReadMessageAsync ();
            Assert.True (message.Type == "result");
            Assert.True (message.Success == true);
            Assert.True (message.Id == 1);

            // Test sequence of Id:s
            wscli.SendMessage (new SubscribeEventMessage { });
            message = await wscli.ReadMessageAsync ();
            Assert.True (message.Id == 2);

            await wscli.DisconnectAsync ();

        }

    }
    public class HomeAssistantMockFixture : IDisposable {
        HomeAssistantMock mock;
        public HomeAssistantMockFixture () {
            mock = new HomeAssistantMock ();
        }
        public void Dispose () {
            mock.Stop ();
        }
    }
}