using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

public class HomeAssistantMock : Hub
{
    public static readonly int RECIEIVE_BUFFER_SIZE = 1024 * 4;
    IHost host = null;
    public HomeAssistantMock()
    {
        host = CreateHostBuilder(null).Build();
        host.Start();
        // host.Start();
    }


    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://127.0.0.1:5001", "http://172.17.0.2:5001");
                webBuilder.UseStartup<HassMockStartup>();
            });
    public void Stop()
    {
        host.StopAsync();
        host.WaitForShutdown();
        host.Dispose();

    }
}

public class HassMockStartup
{
    private readonly string mockTestdataPath = Path.Combine(AppContext.BaseDirectory.Substring(0, AppContext.BaseDirectory.LastIndexOf("/bin")), "Mocks", "testdata");
    public HassMockStartup(IConfiguration configuration)
    {
        Configuration = configuration;

    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {

    }
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var webSocketOptions = new WebSocketOptions()
        {
            KeepAliveInterval = TimeSpan.FromSeconds(120),
            ReceiveBufferSize = HomeAssistantMock.RECIEIVE_BUFFER_SIZE
        };
        app.UseWebSockets(webSocketOptions);
        app.Map("/api/websocket", builder =>
                    {
                        builder.Use(async (context, next) =>
                        {
                            if (context.WebSockets.IsWebSocketRequest)
                            {
                                var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                                await ProcessWS(webSocket);
                                return;
                            }

                            await next();
                        });
                    });

    }

    /// <summary>
    /// Process incoming websocket requests to simulate Home Assistant websocket API
    /// </summary>
    private async Task ProcessWS(WebSocket webSocket)
    {
        // Buffer is set.
        byte[] buffer = new byte[HomeAssistantMock.RECIEIVE_BUFFER_SIZE];

        // First send auth required to the client
        byte[] authRequiredMessage = File.ReadAllBytes(Path.Combine(this.mockTestdataPath, "auth_required.json"));
        await webSocket.SendAsync(new ArraySegment<byte>(authRequiredMessage, 0, authRequiredMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);

        // Wait for incoming messages
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        while (!result.CloseStatus.HasValue)
        {
            var x = JsonSerializer.Deserialize<HassMessage>(new ReadOnlySpan<byte>(buffer, 0, result.Count));
            await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
            // Wait for incoming messages
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }

    private class HassMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    private class AuthMessage : HassMessage
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
    }

}