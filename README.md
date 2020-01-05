# .NET Home Assistant client

This is the client for Home Assistant written in .NET core 3.1. The component is tested in windows and linux.

The project is very early but basic functionality is there. The component is in alpha so major changes can be
done since the design is not set yet.

## Installing HassClient component

The component is available as nuget package.

## Using the component

Create a new console application
`dotnet new console`

## Add the nuget package to the project

```sh
dotnet add package JoySoftware.HassClient --version 0.0.7-alpha
```

The follwing code snippet can be used. The project will add more real example projects at a later time.

```cs

using System;
using System.Threading.Tasks;
using JoySoftware.HomeAssistant.Client;

namespace nethassclienttest
{
    class Program
    {
        private static Task _homeAssistantTask;
        private static HassClient client;

        public static async Task Main(string[] args)
        {
            _homeAssistantTask = ConnectToHomeAssistant();
            Console.ReadLine();

            if (client != null)
            {
                Console.WriteLine("Closing connection...");
                await client.CloseAsync();
            }
        }

        private static async Task ConnectToHomeAssistant()
        {
            string ip = "192.168.1.100";    // Replace with your ip
            short port = 8123;              // Replace with your port
            string token = "YOUR TOKEN";    // Replace with hass token

            client = new HassClient();
            // Connect with ip and port, no ssl and provided token.
            // Also fetch all states on connect
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

            // Subscribe to all events
            await client.SubscribeToEvents();

            var callServiceOk = await client.CallService("light", "toggle", new { entity_id = "light.tomas_rum" });

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
    }
}

}
```
