using HassClient.Performance.Tests.Mocks;
using System;
using System.Threading.Tasks;

namespace HassClient.Performance.Tests
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            Task connectToHomeAssistantTask = null;

            //System.CommandLine.

            //if (string.IsNullOrEmpty(input))
            //    printUsage();

            //switch (args[0])
            //{
            //    case "-p":
            //        await DoPerformanceTest();
            //        break;
            //    case "-c":
            //        connectToHomeAssistantTask = ConnectToHomeAssistant();
            //        break;
            //}
            Console.ReadLine();
        }

        private static Task ConnectToHomeAssistant()
        {
            var host = "localhost";
            var port = 8123;
            var token = "no token";

            return Task.Delay(1000);

        }

        private static void printUsage()
        {
            string usage = @"
Please use following commands:
    -p      Runs the internal performance test
    -c      Connects to home assistant with the provided ip address, port and key
            Example:
            -c ip:192.168.1.10 port:8123 token:myhasstoken
";

            Console.WriteLine(usage);

        }

        private static async Task DoPerformanceTest()
        {
            using var mock = new HomeAssistantMockHandler();
            int NR_OF_REQUESTS = 200000;
            var wscli = new HassClient();
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false, false);

            var wscli2 = new HassClient();
            bool result2 = await wscli2.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false, false);

            var wscli3 = new HassClient();
            bool result3 = await wscli3.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false, false);

            var wscli4 = new HassClient();
            bool result4 = await wscli4.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false, false);

            var wscli5 = new HassClient();
            bool result5 = await wscli5.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false, false);

            var wscli6 = new HassClient();
            bool result6 = await wscli6.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ", false, false);

            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            var first = Task.Run(async () =>
            {

                for (int i = 0; i < NR_OF_REQUESTS; i++)
                {
                    await wscli.PingAsync(1000);

                }
            });
            var second = Task.Run(async () =>
            {
                for (int i = 0; i < NR_OF_REQUESTS; i++)
                {
                    await wscli2.PingAsync(1000);

                }
            });

            var third = Task.Run(async () =>
            {
                for (int i = 0; i < NR_OF_REQUESTS; i++)
                {
                    await wscli3.PingAsync(1000);

                }
            });

            var fourth = Task.Run(async () =>
            {
                for (int i = 0; i < NR_OF_REQUESTS; i++)
                {
                    await wscli4.PingAsync(1000);

                }
            });

            var fifth = Task.Run(async () =>
            {
                for (int i = 0; i < NR_OF_REQUESTS; i++)
                {
                    await wscli5.PingAsync(1000);

                }
            });

            var sixth = Task.Run(async () =>
            {
                for (int i = 0; i < NR_OF_REQUESTS; i++)
                {
                    await wscli6.PingAsync(1000);

                }
            });

            Console.WriteLine("WAIT ALL");
            Task.WaitAll(first); //, second, third, fourth, fifth, sixth
            stopWatch.Stop();
            Console.WriteLine(stopWatch.ElapsedMilliseconds);
            Console.WriteLine("Took {0} seconds with performance of {1} roundtrips/s", stopWatch.ElapsedMilliseconds / 1000, NR_OF_REQUESTS * 6 / (stopWatch.ElapsedMilliseconds / 1000));
            Console.WriteLine("DISCONNECTS!");
            await wscli.CloseAsync();
            await wscli2.CloseAsync();
            await wscli3.CloseAsync();
            await wscli4.CloseAsync();
            await wscli5.CloseAsync();
            await wscli6.CloseAsync();
        }
    }

    public class HomeAssistantMockHandler : IDisposable
    {
        private readonly HomeAssistantMock mock;
        public HomeAssistantMockHandler() => mock = new HomeAssistantMock();
        public void Dispose() => mock.Stop();
    }
}