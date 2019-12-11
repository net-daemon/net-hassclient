using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// The Home Assistant Mock class implements a fake Home Assistant server by
/// exposing the websocket api and fakes responses to requests.
/// </summary>
public class HomeAssistantMock {
    public static readonly int RECIEIVE_BUFFER_SIZE = 1024 * 4;
    IHost host = null;

    public HomeAssistantMock () {
        host = CreateHostBuilder ().Build ();
        host.Start ();
    }

    /// <summary>
    /// Starts a websocket server in a generic host
    /// </summary>
    /// <returns>Returns a IHostBuilder instance</returns>
    public static IHostBuilder CreateHostBuilder () =>
        Host.CreateDefaultBuilder ()
        .ConfigureWebHostDefaults (webBuilder => {
            webBuilder.UseUrls ("http://127.0.0.1:5001", "http://172.17.0.2:5001");
            webBuilder.UseStartup<HassMockStartup> ();
        });

    /// <summary>
    /// Stops the fake Home Assistant server
    /// </summary>
    public void Stop () {
        host.StopAsync ();
        host.WaitForShutdown ();
        host.Dispose ();

    }
}
/// <summary>
/// The class implementing the mock hass server
/// </summary>
public class HassMockStartup {
    // Get the path to mock testdata
    private readonly string mockTestdataPath = Path.Combine (AppContext.BaseDirectory.Substring (0, AppContext.BaseDirectory.LastIndexOf ("/bin")), "Mocks", "testdata");
    // Home Assistant will always prettyprint responses so so do the mock
    private readonly byte[] authOkMessage = File.ReadAllBytes (Path.Combine (AppContext.BaseDirectory.Substring (0, AppContext.BaseDirectory.LastIndexOf ("/bin")), "Mocks", "testdata", "auth_ok.json"));
    private JsonSerializerOptions serializeOptions = new JsonSerializerOptions {
        WriteIndented = true
    };

    public HassMockStartup (IConfiguration configuration) {
        Configuration = configuration;

    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices (IServiceCollection services) {

    }
    public void Configure (IApplicationBuilder app, IWebHostEnvironment env) {
        var webSocketOptions = new WebSocketOptions () {
            KeepAliveInterval = TimeSpan.FromSeconds (120),
            ReceiveBufferSize = HomeAssistantMock.RECIEIVE_BUFFER_SIZE
        };
        app.UseWebSockets (webSocketOptions);
        app.Map ("/api/websocket", builder => {
            builder.Use (async (context, next) => {
                if (context.WebSockets.IsWebSocketRequest) {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync ();

                    await ProcessWS (webSocket);
                    return;
                }

                await next ();
            });
        });

    }

    /// <summary>
    /// Process incoming websocket requests to simulate Home Assistant websocket API
    /// </summary>
    private async Task ProcessWS (WebSocket webSocket) {
        // Buffer is set.
        byte[] buffer = new byte[HomeAssistantMock.RECIEIVE_BUFFER_SIZE];

        try {
            // First send auth required to the client
            byte[] authRequiredMessage = File.ReadAllBytes (Path.Combine (this.mockTestdataPath, "auth_required.json"));
            await webSocket.SendAsync (new ArraySegment<byte> (authRequiredMessage, 0, authRequiredMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);

            // Wait for incoming messages
            var result = await webSocket.ReceiveAsync (new ArraySegment<byte> (buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue) {
                var hassMessag = JsonSerializer.Deserialize<HassMessage> (new ReadOnlySpan<byte> (buffer, 0, result.Count));
                switch (hassMessag.Type) {
                    // We have an auth message
                    case "auth":
                        var authMessage = JsonSerializer.Deserialize<AuthMessage> (new ReadOnlySpan<byte> (buffer, 0, result.Count));
                        if (authMessage.AccessToken == "ABCDEFGHIJKLMNOPQ") {
                            // Hardcoded to be correct for testcase
                            // byte[] authOkMessage = File.ReadAllBytes (Path.Combine (this.mockTestdataPath, "auth_ok.json"));
                            await webSocket.SendAsync (new ArraySegment<byte> (authOkMessage, 0, authOkMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                        } else {
                            // Hardcoded to be correct for testcase
                            byte[] authNotOkMessage = File.ReadAllBytes (Path.Combine (this.mockTestdataPath, "auth_notok.json"));
                            await webSocket.SendAsync (new ArraySegment<byte> (authNotOkMessage, 0, authNotOkMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                            // Hass will normally close session here but for the sake of testing it wont
                        }
                        break;
                    case "subscribe_events":
                        var subscribeEventMessage = JsonSerializer.Deserialize<SubscribeEventMessage> (new ReadOnlySpan<byte> (buffer, 0, result.Count));
                        var response = new ResultMessage {
                            Id = subscribeEventMessage.Id
                        };

                        var responseString = JsonSerializer.SerializeToUtf8Bytes (response, typeof (ResultMessage), this.serializeOptions);
                        await webSocket.SendAsync (new ArraySegment<byte> (responseString, 0, responseString.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                        break;

                }
                // Wait for incoming messages
                result = await webSocket.ReceiveAsync (new ArraySegment<byte> (buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync (result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        } catch (System.OperationCanceledException) {
            // Normal when server is stopped
        } catch (System.Exception e) {

            throw new ApplicationException ("The thing is closed unexpectedly", e);
        }

    }

    private class HassMessage {
        [JsonPropertyName ("type")]
        public string Type { get; set; }
    }

    private class AuthMessage : HassMessage {
        [JsonPropertyName ("access_token")]
        public string AccessToken { get; set; }
    }

    private class SubscribeEventMessage : HassMessage {
        [JsonPropertyName ("id")]
        public int Id { get; set; }
    }

    private class ResultMessage {
        [JsonPropertyName ("id")]
        public int Id { get; set; }

        [JsonPropertyName ("type")]
        public string Type { get; set; } = "result";

        [JsonPropertyName ("success")]
        public Boolean Success { get; set; } = true;

        [JsonPropertyName ("result")]
        public Object Result { get; set; }
    }
}