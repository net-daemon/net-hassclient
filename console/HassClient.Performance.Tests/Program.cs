using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Threading.Tasks;
using HassClientIntegrationTests.Mocks;
using Microsoft.Extensions.Logging;

namespace JoySoftware.HomeAssistant.Client.Performance.Tests
{
    internal class Program
    {
        private static Task _homeAssistantTask;
        private static HassClient client;

        public static async Task Main(string[] args)
        {
            var cmd = new RootCommand();
            cmd.AddCommand(connectHass());
            int result = cmd.InvokeAsync(args).Result;

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

            cmd.AddOption(new Option(new[] {"--ip", "-i"}, "IP address of Hass")
            {
                Argument = new Argument<string>(() => "localhost") {Arity = ArgumentArity.ExactlyOne}
            });
            cmd.AddOption(new Option(new[] {"--port", "-p"}, "Port of Hass")
            {
                Argument = new Argument<short>(() => 8123) {Arity = ArgumentArity.ExactlyOne}
            });
            cmd.AddOption(new Option(new[] {"--events", "-e"}, "Get events!")
            {
                Argument = new Argument<bool> {Arity = ArgumentArity.ExactlyOne}
            });
            cmd.AddOption(new Option(new[] {"--token", "-t"}, "Access token")
            {
                Argument = new Argument<string> {Arity = ArgumentArity.ExactlyOne}
            });
            cmd.Handler = CommandHandler.Create<string, short, bool, string>((ip, port, events, token) =>
            {
                _homeAssistantTask = Task.Run(() => ConnectToHomeAssistant(ip, port, events, token));
            });
            return cmd;
        }

        private static async Task ConnectToHomeAssistant(string ip, short port, bool events, string token)
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder
                    .ClearProviders()
                    .AddFilter("HassClient.HassClient", LogLevel.Trace)
                    .AddConsole();
            });

            client = new HassClient(factory);
            var token_env = Environment.GetEnvironmentVariable("HASS_TOKEN");
            if (token_env != null)
                token = token_env;

            bool connected = await client.ConnectAsync(ip, port, false, token, true);
            if (!connected)
            {
                Console.WriteLine("Failed to connect to Home assistant.. bailing...");
                return;
            }

            Console.WriteLine("Login success");
            if (client.States != null)
            {
                Console.WriteLine($"Number of states: {client.States.Count}");
            }

            var tempTest = client.States["sensor.frysnere_temperature"];
            if (events)
            {
                // Subscribe to all events
                await client.SubscribeToEvents();
            }

            //var test = await client.CallService("light", "toggle", new { entity_id = "light.tomas_rum" });
            //var tt = await client.SetState("sensor.csharp", "cool", new {daemon = true});

            var result = await client.SendEvent("test_event", new { data="hello" });
            while (true)
            {
                try
                {
                    HassEvent eventMsg = await client.ReadEventAsync();

                    //Console.WriteLine($"Eventtype: {eventMsg.EventType}");
                    if (eventMsg.EventType == "state_changed")
                    {
                        var stateMessage = eventMsg?.Data as HassStateChangedEventData;

                        Console.WriteLine(
                            $"{stateMessage.EntityId}: {stateMessage.OldState.State}->{stateMessage.NewState.State}");
                    }
                    else if (eventMsg.EventType == "call_service")
                    {
                        var serviceMessage = eventMsg?.Data as HassServiceEventData;
                        Console.WriteLine($"{serviceMessage.Service}: {serviceMessage.ServiceData}");
                    }
                    else
                    {
                        Console.WriteLine("event!");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful
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
            bool result = await wscli.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);

            var wscli2 = new HassClient();
            bool result2 = await wscli2.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);

            var wscli3 = new HassClient();
            bool result3 = await wscli3.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);

            var wscli4 = new HassClient();
            bool result4 = await wscli4.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);

            var wscli5 = new HassClient();
            bool result5 = await wscli5.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);

            var wscli6 = new HassClient();
            bool result6 = await wscli6.ConnectAsync(new Uri("ws://127.0.0.1:5001/api/websocket"), "ABCDEFGHIJKLMNOPQ",
                false);

            var stopWatch = Stopwatch.StartNew();
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
            Console.WriteLine("Took {0} seconds with performance of {1} roundtrips/s",
                stopWatch.ElapsedMilliseconds / 1000, NR_OF_REQUESTS * 6 / (stopWatch.ElapsedMilliseconds / 1000));
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

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    mock.Stop();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion
    }
}