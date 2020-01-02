using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace HassClientIntegrationTests.Mocks
{
    /// <summary>
    ///     The Home Assistant Mock class implements a fake Home Assistant server by
    ///     exposing the websocket api and fakes responses to requests.
    /// </summary>
    public class HomeAssistantMock : IDisposable
    {
        public static readonly int RecieiveBufferSize = 1024 * 4;
        private readonly IHost _host;

        public HomeAssistantMock()
        {
            _host = CreateHostBuilder().Build();
            _host.Start();
        }

        /// <summary>
        ///     Starts a websocket server in a generic host
        /// </summary>
        /// <returns>Returns a IHostBuilder instance</returns>
        public static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://127.0.0.1:5001"); //"http://172.17.0.2:5001"
                    webBuilder.UseStartup<HassMockStartup>();
                });

        /// <summary>
        ///     Stops the fake Home Assistant server
        /// </summary>
        public void Stop()
        {
            _host.StopAsync();
            _host.WaitForShutdown();
        }

        public void Dispose() => _host?.Dispose();
    }

    /// <summary>
    ///     The class implementing the mock hass server
    /// </summary>
    public class HassMockStartup
    {
        // Home Assistant will always prettyprint responses so so do the mock
        private readonly byte[] _authOkMessage =
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Mocks", "testdata", "auth_ok.json"));

        // Get the path to mock testdata
        private readonly string _mockTestdataPath = Path.Combine(AppContext.BaseDirectory, "Mocks", "testdata");

        private readonly byte[] _pongMessage =
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Mocks", "testdata", "pong.json"));

        private readonly JsonSerializerOptions serializeOptions = new JsonSerializerOptions { WriteIndented = true };

        public HassMockStartup(IConfiguration configuration) => Configuration = configuration;

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = HomeAssistantMock.RecieiveBufferSize
            };
            app.UseWebSockets(webSocketOptions);
            app.Map("/api/websocket", builder =>
            {
                builder.Use(async (context, next) =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                        await ProcessWS(webSocket);
                        return;
                    }

                    await next();
                });
            });
        }

        /// <summary>
        ///     Process incoming websocket requests to simulate Home Assistant websocket API
        /// </summary>
        private async Task ProcessWS(WebSocket webSocket)
        {
            // Buffer is set.
            byte[] buffer = new byte[HomeAssistantMock.RecieiveBufferSize];

            try
            {
                // First send auth required to the client
                byte[] authRequiredMessage = File.ReadAllBytes(Path.Combine(_mockTestdataPath, "auth_required.json"));
                await webSocket.SendAsync(new ArraySegment<byte>(authRequiredMessage, 0, authRequiredMessage.Length),
                    WebSocketMessageType.Text, true, CancellationToken.None);

                // Wait for incoming messages
                WebSocketReceiveResult result =
                    await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                while (!result.CloseStatus.HasValue)
                {
                    HassMessage hassMessage =
                        JsonSerializer.Deserialize<HassMessage>(new ReadOnlySpan<byte>(buffer, 0, result.Count));
                    switch (hassMessage.Type)
                    {
                        // We have an auth message
                        case "auth":
                            AuthMessage authMessage =
                                JsonSerializer.Deserialize<AuthMessage>(
                                    new ReadOnlySpan<byte>(buffer, 0, result.Count));
                            if (authMessage.AccessToken == "ABCDEFGHIJKLMNOPQ")
                            {
                                // Hardcoded to be correct for test-case
                                // byte[] authOkMessage = File.ReadAllBytes (Path.Combine (this.mockTestdataPath, "auth_ok.json"));
                                await webSocket.SendAsync(
                                    new ArraySegment<byte>(_authOkMessage, 0, _authOkMessage.Length),
                                    WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            else
                            {
                                // Hardcoded to be correct for test-case
                                byte[] authNotOkMessage =
                                    File.ReadAllBytes(Path.Combine(_mockTestdataPath, "auth_notok.json"));
                                await webSocket.SendAsync(
                                    new ArraySegment<byte>(authNotOkMessage, 0, authNotOkMessage.Length),
                                    WebSocketMessageType.Text, true, CancellationToken.None);
                                // Hass will normally close session here but for the sake of testing it wont
                            }

                            break;
                        case "ping":
                            // Hardcoded to be correct for performance tests
                            await webSocket.SendAsync(new ArraySegment<byte>(_pongMessage, 0, _pongMessage.Length),
                                WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case "subscribe_events":
                            SendCommandMessage subscribeEventMessage =
                                JsonSerializer.Deserialize<SendCommandMessage>(
                                    new ReadOnlySpan<byte>(buffer, 0, result.Count));
                            var response = new ResultMessage { Id = subscribeEventMessage.Id };
                            // First send normal ok response
                            byte[] responseString =
                                JsonSerializer.SerializeToUtf8Bytes(response, typeof(ResultMessage), serializeOptions);
                            await webSocket.SendAsync(new ArraySegment<byte>(responseString, 0, responseString.Length),
                                WebSocketMessageType.Text, true, CancellationToken.None);
                            // For tests send a event message
                            byte[] newEventMessage = File.ReadAllBytes(Path.Combine(_mockTestdataPath, "event.json"));
                            await webSocket.SendAsync(
                                new ArraySegment<byte>(newEventMessage, 0, newEventMessage.Length),
                                WebSocketMessageType.Text, true, CancellationToken.None);
                            // Hass will normally close session here but for the sake of testing it wont

                            break;

                        case "get_states":
                            JsonSerializer.Deserialize<SendCommandMessage>(
                                new ReadOnlySpan<byte>(buffer, 0, result.Count));
                            byte[] stateReusultMessage =
                                File.ReadAllBytes(Path.Combine(_mockTestdataPath, "result_states.json"));
                            await webSocket.SendAsync(
                                new ArraySegment<byte>(stateReusultMessage, 0, stateReusultMessage.Length),
                                WebSocketMessageType.Text, true, CancellationToken.None);

                            break;

                        case "fake_disconnect_test":
                            // This is not a real home assistant message, just used to test disconnect from socket.
                            // This one tests a normal disconnect
                            var timeout = new CancellationTokenSource(5000);
                            try
                            {
                                // Send close message (some bug n CloseAsync makes we have to do it this way)
                                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                                    timeout.Token);
                                // Wait for close message
                                await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                            }
                            catch (OperationCanceledException)
                            {
                            }

                            return;
                    }

                    // Wait for incoming messages
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }

                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription,
                    CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Normal when server is stopped
            }
            catch (Exception e)
            {
                throw new ApplicationException("The thing is closed unexpectedly", e);
            }
        }

        private class HassMessage
        {
            [JsonPropertyName("type")] public string Type { get; set; }
        }

        private class AuthMessage : HassMessage
        {
            [JsonPropertyName("access_token")] public string AccessToken { get; set; }
        }

        private class SendCommandMessage : HassMessage
        {
            [JsonPropertyName("id")] public int Id { get; set; }
        }

        private class ResultMessage
        {
            [JsonPropertyName("id")] public int Id { get; set; }

            // ReSharper disable once UnusedMember.Local
            [JsonPropertyName("type")] public string Type { get; set; } = "result";

            // ReSharper disable once UnusedMember.Local
            [JsonPropertyName("success")] public bool Success { get; set; } = true;

            // ReSharper disable once UnusedMember.Local
            [JsonPropertyName("result")] public object Result { get; set; } = "some result";
        }
    }
}