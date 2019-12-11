using System;
using System.Threading.Tasks;
using HassClient;
using HassClient.Performance.Tests.Mocks;
using Xunit;
using Xunit.Abstractions;

namespace HassClient.Performance.Tests {
    public class UnitTest1 : IClassFixture<HomeAssistantMockFixture> {
        HomeAssistantMockFixture mockFixture;
        private readonly ITestOutputHelper output;
        public UnitTest1 (HomeAssistantMockFixture fixture, ITestOutputHelper output) {
            mockFixture = fixture;
            this.output = output;
        }

        [Fact]
        async public void Test1 () {

            Assert.False (false);
            // 'await Task.Delay (10000);

        }

        [Fact]
        public async void Test2 () {
            WSClient wscli = new WSClient ();
            var result = await wscli.ConnectAsync (new Uri ("ws://127.0.0.1:5001/api/websocket"));
            var message = await wscli.WaitForMessage ();

            Assert.True (message.Type == "auth_required");
            await wscli.SendMessage (new AuthMessage { AccessToken = "WRONG PASSWORD" });
            message = await wscli.WaitForMessage ();
            Assert.True (message.Type == "auth_invalid");

            await wscli.SendMessage (new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            message = await wscli.WaitForMessage ();
            Assert.True (message.Type == "auth_ok");
            await wscli.DisconnectAsync ();

        }

        [Fact]
        public async void TestPerformance () {
            var NR_OF_REQUESTS = 100000;
            WSClient wscli = new WSClient ();
            var result = await wscli.ConnectAsync (new Uri ("ws://127.0.0.1:5001/api/websocket"));
            var message = await wscli.WaitForMessage ();
            var stopWatch = System.Diagnostics.Stopwatch.StartNew ();
            Task first = Task.Run (async () => {
                for (int i = 0; i < NR_OF_REQUESTS; i++) {
                    await wscli.SendMessage (new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
                    message = await wscli.WaitForMessage ();

                }
            });
            Task second = Task.Run (async () => {
                for (int i = 0; i < NR_OF_REQUESTS; i++) {
                    await wscli.SendMessage (new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
                    message = await wscli.WaitForMessage ();

                }
            });
            Task.WaitAll (first, second);
            stopWatch.Stop ();
            output.WriteLine ("TOMAS");
            output.WriteLine ("Took {0} seconds with performance of {1} roundtrips/s", stopWatch.ElapsedMilliseconds / 1000, NR_OF_REQUESTS / (stopWatch.ElapsedMilliseconds / 1000));
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