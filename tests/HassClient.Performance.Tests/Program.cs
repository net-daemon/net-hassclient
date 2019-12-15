using System;
using System.Threading;
using System.Threading.Tasks;
using HassClient;
using HassClient.Performance.Tests.Mocks;

namespace HassClient.Performance.Tests
{
    class Program
    {
        public static async Task Main()
        {
            using var mock = new HomeAssistantMockHandler();
            var NR_OF_REQUESTS = 100000;
            WSClient wscli = new WSClient();
            var result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            var message = await wscli.ReadMessageAsync();

            WSClient wscli2 = new WSClient();
            var result2 = await wscli2.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            var message2 = await wscli2.ReadMessageAsync();

            WSClient wscli3 = new WSClient();
            var result3 = await wscli3.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"));
            var message3 = await wscli3.ReadMessageAsync();

            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            Task first = Task.Run(async () =>
            {
                for (int i = 0; i < NR_OF_REQUESTS; i++)
                {
                    wscli.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
                    message = await wscli.ReadMessageAsync();

                }
            });
            Task second = Task.Run(async () =>
            {
                for (int i = 0; i < NR_OF_REQUESTS; i++)
                {
                    wscli2.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
                    message2 = await wscli2.ReadMessageAsync();

                }
            });

            Task third = Task.Run(async () =>
            {
                for (int i = 0; i < NR_OF_REQUESTS; i++)
                {
                    wscli3.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
                    message3 = await wscli3.ReadMessageAsync();

                }
            });


            Console.WriteLine("WAIT ALL");
            Task.WaitAll(first, second, third);
            stopWatch.Stop();
            Console.WriteLine("Took {0} seconds with performance of {1} roundtrips/s", stopWatch.ElapsedMilliseconds / 1000, NR_OF_REQUESTS / (stopWatch.ElapsedMilliseconds / 1000));
            Console.WriteLine("DISCONNECTS!");
            await wscli.CloseAsync();
            await wscli2.CloseAsync();
            await wscli3.CloseAsync();
        }
    }

    public class HomeAssistantMockHandler : IDisposable
    {
        HomeAssistantMock mock;
        public HomeAssistantMockHandler()
        {
            mock = new HomeAssistantMock();
        }
        public void Dispose()
        {
            mock.Stop();
        }
    }
}