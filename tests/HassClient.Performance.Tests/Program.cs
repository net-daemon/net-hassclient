using System;
using System.Threading;
using System.Threading.Tasks;
using HassClient;
using HassClient.Performance.Tests.Mocks;

namespace HassClient.Performance.Tests {
    class Program {
        public static async Task Main () {
            using var mock = new HomeAssistantMockHandler ();
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
            Console.WriteLine ("Took {0} seconds with performance of {1} roundtrips/s", stopWatch.ElapsedMilliseconds / 1000, NR_OF_REQUESTS / (stopWatch.ElapsedMilliseconds / 1000));
            await wscli.DisconnectAsync ();
        }
    }

    public class HomeAssistantMockHandler : IDisposable {
        HomeAssistantMock mock;
        public HomeAssistantMockHandler () {
            mock = new HomeAssistantMock ();
        }
        public void Dispose () {
            mock.Stop ();
        }
    }
}