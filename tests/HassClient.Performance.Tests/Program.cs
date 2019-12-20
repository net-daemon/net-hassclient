using HassClient.Performance.Tests.Mocks;
using System;
using System.Threading.Tasks;

namespace HassClient.Performance.Tests
{
    internal class Program
    {
        public static async Task Main()
        {
            //using var mock = new HomeAssistantMockHandler();
            //int NR_OF_REQUESTS = 200000;
            //var wscli = new HassClient();
            //bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
            //HassMessage message = await wscli.ReadMessageAsync();

            //var wscli2 = new HassClient();
            //bool result2 = await wscli2.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
            //HassMessage message2 = await wscli2.ReadMessageAsync();

            //var wscli3 = new HassClient();
            //bool result3 = await wscli3.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
            //HassMessage message3 = await wscli3.ReadMessageAsync();

            //var wscli4 = new HassClient();
            //bool result4 = await wscli4.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
            //HassMessage message4 = await wscli4.ReadMessageAsync();

            //var wscli5 = new HassClient();
            //bool result5 = await wscli5.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
            //HassMessage message5 = await wscli5.ReadMessageAsync();

            //var wscli6 = new HassClient();
            //bool result6 = await wscli6.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ");
            //HassMessage message6 = await wscli6.ReadMessageAsync();

            //var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            //var first = Task.Run(async () =>
            //{
            //    for (int i = 0; i < NR_OF_REQUESTS; i++)
            //    {
            //        wscli.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            //        message = await wscli.ReadMessageAsync();

            //    }
            //});
            //var second = Task.Run(async () =>
            //{
            //    for (int i = 0; i < NR_OF_REQUESTS; i++)
            //    {
            //        wscli2.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            //        message2 = await wscli2.ReadMessageAsync();

            //    }
            //});

            //var third = Task.Run(async () =>
            //{
            //    for (int i = 0; i < NR_OF_REQUESTS; i++)
            //    {
            //        wscli3.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            //        message3 = await wscli3.ReadMessageAsync();

            //    }
            //});

            //var fourth = Task.Run(async () =>
            //{
            //    for (int i = 0; i < NR_OF_REQUESTS; i++)
            //    {
            //        wscli4.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            //        message4 = await wscli4.ReadMessageAsync();

            //    }
            //});

            //var fifth = Task.Run(async () =>
            //{
            //    for (int i = 0; i < NR_OF_REQUESTS; i++)
            //    {
            //        wscli5.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            //        message5 = await wscli5.ReadMessageAsync();

            //    }
            //});

            //var sixth = Task.Run(async () =>
            //{
            //    for (int i = 0; i < NR_OF_REQUESTS; i++)
            //    {
            //        wscli6.SendMessage(new AuthMessage { AccessToken = "ABCDEFGHIJKLMNOPQ" });
            //        message6 = await wscli6.ReadMessageAsync();

            //    }
            //});

            //Console.WriteLine("WAIT ALL");
            //Task.WaitAll(first, second, third, fourth, fifth, sixth);
            //stopWatch.Stop();
            //Console.WriteLine(stopWatch.ElapsedMilliseconds);
            //Console.WriteLine("Took {0} seconds with performance of {1} roundtrips/s", stopWatch.ElapsedMilliseconds / 1000, NR_OF_REQUESTS * 6 / (stopWatch.ElapsedMilliseconds / 1000));
            //Console.WriteLine("DISCONNECTS!");
            //await wscli.CloseAsync();
            //await wscli2.CloseAsync();
            //await wscli3.CloseAsync();
            //await wscli4.CloseAsync();
            //await wscli5.CloseAsync();
            //await wscli6.CloseAsync();
        }
    }

    public class HomeAssistantMockHandler : IDisposable
    {
        private readonly HomeAssistantMock mock;
        public HomeAssistantMockHandler() => mock = new HomeAssistantMock();
        public void Dispose() => mock.Stop();
    }
}