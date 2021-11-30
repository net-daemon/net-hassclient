using System;
using System.Threading;
using System.Threading.Tasks;

namespace JoySoftware.HomeAssistant.Client.TestService;


internal class DebugService : BackgroundService
{
    private readonly IHassClient _hassClient;
    private readonly HomeAssistantSettings _haSettings;
    public DebugService(
        IHassClient hassClient,
        IOptions<HomeAssistantSettings> settings,
        IObservable<HassEvent> events,
        IObservable<ConnectionStatus> connectionStatus,
        ILogger<DebugService> logger)
    {
        _haSettings = settings.Value;
        _hassClient = hassClient;

        events.Subscribe(e => HandleEvent(e, logger));
        connectionStatus.Subscribe(e => HandleConnectionStatus(e, logger));
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _hassClient.Run(
                    _haSettings.Host,
                    _haSettings.Port,
                    _haSettings.Ssl,
                    _haSettings.Token,
                    stoppingToken);
    }

    private static void HandleEvent(HassEvent hassEvent, ILogger logger)
    {
        logger.LogDebug("New event ({type})", hassEvent.EventType);
    }

    private static void HandleConnectionStatus(ConnectionStatus connectionStatus, ILogger logger)
    {
        logger.LogInformation("Connection changed status ({connectionStatus})", connectionStatus);
    }
}