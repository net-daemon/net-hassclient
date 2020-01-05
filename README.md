# .NET Home Assistant client

This is the client for Home Assistant written in .NET core 3.1. The component is tested in windows and linux.

## Installing HassClient component

The component is available as nuget package.

```sh
dotnet add package JoySoftware.HassClient --version 0.0.6-alpha
```

## Using the component

The follwing code snippet can be used in a async method like in `public static async Task Main(string[] args)`. The project will add example projects later.

```cs
    string ip = "127.0.0.1";    // Replace with your ip
    short port = 8123;          // Replace with your port
    string token = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; // Replace with hass token

    client = new HassClient();
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

    if (events)
    {
        // Subscribe to all events
        await client.SubscribeToEvents();
    }

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

```
