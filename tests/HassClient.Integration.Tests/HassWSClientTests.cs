using System;
using System.Threading.Tasks;
using HassClient;
using Xunit;
namespace NetDaemonLibTest {
    public class UnitTest1 : IClassFixture<HomeAssistantMockFixture> {
        HomeAssistantMockFixture mockFixture;

        public UnitTest1 (HomeAssistantMockFixture fixture) {
            mockFixture = fixture;
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
            var NR_OF_REQUESTS = 800000;
            WSClient wscli = new WSClient ();
            var result = await wscli.ConnectAsync (new Uri ("ws://127.0.0.1:5001/api/websocket"));
            var message = await wscli.WaitForMessage ();
            var stopWatch = System.Diagnostics.Stopwatch.StartNew ();
            for (int i = 0; i < NR_OF_REQUESTS; i++) {
                await wscli.SendMessage (new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
                message = await wscli.WaitForMessage ();

            }
            stopWatch.Stop ();
            System.IO.File.WriteAllText ("/tmp/perfresult.txt", $"Took {stopWatch.ElapsedMilliseconds / 1000} seconds with performance of {NR_OF_REQUESTS / (stopWatch.ElapsedMilliseconds / 1000)} roundtrips/s");
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