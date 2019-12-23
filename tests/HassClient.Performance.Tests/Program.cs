using HassClient.Performance.Tests.Mocks;
using Microsoft.Extensions.Logging;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace HassClient.Performance.Tests
{
    internal class Program
    {


        static Task _homeAssistantTask = null;
        static HassClient client = null;

        public static async Task Main(string[] args)
        {
            var cmd = new RootCommand();
            cmd.AddCommand(connectHass());
            var result = cmd.InvokeAsync(args).Result;
            //// Create a root command with some options
            //var rootCommand = new RootCommand
            //{
            //    new Option(
            //        "--int-option",
            //        "An option whose argument is parsed as an int")
            //    {
            //        Argument = new Argument<int>(defaultValue: () => 42)
            //    },
            //    new Option(
            //        "--bool-option",
            //        "An option whose argument is parsed as a bool")
            //    {
            //        Argument = new Argument<bool>()
            //    },
            //    new Option(
            //        "-d",
            //        "Not parsed as anything")

            //};
            //rootCommand.Description = "My sample app";

            //rootCommand.Handler = CommandHandler.Create<int, bool, int>((intOption, boolOption, someOption) =>
            //{
            //    Console.WriteLine($"The value for --int-option is: {intOption}");
            //    Console.WriteLine($"The value for --bool-option is: {boolOption}");
            //    Console.WriteLine($"The value for --bool-option is: {someOption}");
            //});

            // Parse the incoming args and invoke the handler

            //var result = rootCommand.InvokeAsync(args).Result;

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

            if (client != null)
            {
                Console.WriteLine("Closing connection...");
                await client.CloseAsync();
            }

        }

        private static Command connectHass()
        {
            var cmd = new Command("-c", "Connects to home assistant");

            cmd.AddOption(new Option(new[] { "--ip", "-i" }, "IP address of Hass")
            {
                Argument = new Argument<string>(defaultValue: () => "localhost")
                {
                    Arity = ArgumentArity.ExactlyOne,
                }
            });
            cmd.AddOption(new Option(new[] { "--port", "-p" }, "Port of Hass")
            {
                Argument = new Argument<int>(defaultValue: () => 8123)
                {
                    Arity = ArgumentArity.ExactlyOne,
                }
            });
            cmd.AddOption(new Option(new[] { "--events", "-e" }, "Get events!")
            {
                Argument = new Argument<bool>()
                {
                    Arity = ArgumentArity.ExactlyOne,
                }
            });
            cmd.AddOption(new Option(new[] { "--token", "-t" }, "Access token")
            {
                Argument = new Argument<string>()
                {
                    Arity = ArgumentArity.ExactlyOne,
                }
            });
            cmd.Handler = CommandHandler.Create<string, int, bool, string>((ip, port, events, token) =>
            {
                _homeAssistantTask = Task.Run(() => ConnectToHomeAssistant(ip, port, events, token));
            });
            return cmd;
        }
        private async static Task ConnectToHomeAssistant(string ip, int port, bool events, string token)
        {
            var url = new Uri($"ws://{ip}:{port}/api/websocket");
            Console.WriteLine($"Connecting to {url}...");

            var factory = LoggerFactory.Create(builder =>
            {
                builder
                    .ClearProviders()
                    .AddFilter("HassClient.HassClient", LogLevel.Trace)
                    .AddConsole();
            });

            client = new HassClient(logFactory: factory);
            var connected = await client.ConnectAsync(url, token, true, events);
            if (!connected)
            {
                Console.WriteLine("Failed to connect to Home assistant.. bailing...");
                return;
            }
            else
            {
                Console.WriteLine("Login success");
            }
            if (client.States != null)
            {
                Console.WriteLine($"Number of states: {client.States.Count}");
            }

            while (true)
            {
                try
                {
                    var eventMsg = await client.ReadEventAsync();

                    //Console.WriteLine($"Eventtype: {eventMsg.EventType}");
                    if (eventMsg.EventType == "state_changed")
                    {
                        var stateMessage = eventMsg?.Data as StateChangedEventMessage;

                        Console.WriteLine($"{stateMessage.EntityId}: {stateMessage.OldState.State}->{stateMessage.NewState.State}");
                    }

                }
                catch (OperationCanceledException)
                {
                    // Gracefull 
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error", e);
                    return;
                }


            }
        }
        public static int SayHello()
        {
            Console.WriteLine("Hello!");
            return 0;
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