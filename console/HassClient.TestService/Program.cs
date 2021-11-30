using System;
using System.Configuration;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HassClientIntegrationTests.Mocks;
using JoySoftware.HomeAssistant.Extensions;
using JoySoftware.HomeAssistant.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JoySoftware.HomeAssistant.Client.TestService;
internal class Program
{
    public static async Task Main()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "development");
        var loggerFactory = LoggerFactory.Create(builder => builder
            .ClearProviders()
            .AddFilter("HassClient.HassClient", LogLevel.Debug)
            .SetMinimumLevel(LogLevel.Debug)
            .AddConsole());
        var logger = loggerFactory.CreateLogger("Main");
        var hassClient = new HassClient(loggerFactory);
        var builder = new ConfigurationBuilder()
                      .AddJsonFile($"appsettings.json", true, true)
                      .AddJsonFile($"appsettings.development.json", true, true);

        var config = builder.Build();
        var haSettings = config.GetSection("HomeAssistant")?.Get<HomeAssistantSettings>() ?? new HomeAssistantSettings();

        hassClient.HassEventsObservable.Subscribe(s => HandleEvent(s, logger));
        //    CancellationTokenSource source = new CancellationTokenSource(10000);
        await hassClient.Run(
            haSettings.Host,
            haSettings.Port,
            haSettings.Ssl,
            haSettings.Token,
            CancellationToken.None);
    }
    private static void HandleEvent(HassEvent hassEvent, ILogger logger)
    {
        logger.LogDebug("New event ({type})", hassEvent.EventType);
    }
}