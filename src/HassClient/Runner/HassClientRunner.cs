using JoySoftware.HomeAssistant.Client;


namespace JoySoftware.HomeAssistant.Runner;

/// <summary>
///     Implements maintaining a connection to Home Assistant
/// </summary>
internal class HassClientRunner : IHassClientRunner
{
    private readonly ILogger<HassClientRunner> _logger;

    /// <inheritdoc/>
    public IHassClient HassClient { get; }

    public HassClientRunner(IHassClient hassClient, ILogger<HassClientRunner> logger)
    {
        HassClient = hassClient ?? throw new ArgumentNullException(nameof(hassClient));
        _logger = logger;
    }

    // Set ConnectionTimeout to 30 seconds
    private const int _connectionTimeout = 30000;

    /// <inheritdoc/>
    public async Task Run(string host, short port, bool ssl, string hassToken, CancellationToken cancelToken)
    {
        while (!cancelToken.IsCancellationRequested)
        {
            // Combine the iternal token with external to go out of loop if disconnected
            _logger.LogDebug("Connecting to Home Assistant ...");
            var connectionState = await HassClient.ConnectAsync(host, port, ssl, hassToken, true).ConfigureAwait(false);
            if (!connectionState)
            {
                _logger.LogDebug("Failed to connect to Home Assistant, delaying {_connectionTimeout} s", _connectionTimeout / 1000);
                await Delay(_connectionTimeout, cancelToken).ConfigureAwait(false);
            }
            else
            {
                using var connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                connectionState.DisconnectToken, cancelToken);
                // Connection successful
                await DelayUntilRunningState(connectTokenSource.Token).ConfigureAwait(false);
                if (await HassClient.SubscribeToEvents().ConfigureAwait(false))
                {
                    // Just wait until someone stops the HassClient
                    _logger.LogDebug("Connection to Home Assistant successful!");
                    // await Task.Delay(-1, connectTokenSource.Token);
                    try
                    {
                        await connectTokenSource.Token.AsTask().ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogDebug("Connection to Home Assistant cancelled!");
                    }
                    finally
                    {
                        await HassClient.CloseAsync().ConfigureAwait(false);
                    }
                    if (!cancelToken.IsCancellationRequested)
                    {
                        _logger.LogDebug("Connection to Home Assistant disconnected, delaying {_connectionTimeout} s before reconnect ...", _connectionTimeout / 1000);
                        await Delay(_connectionTimeout, cancelToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await HassClient.CloseAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private async Task DelayUntilRunningState(CancellationToken cancelToken)
    {
        bool hasRetried = false;
        while (!cancelToken.IsCancellationRequested)
        {
            var hassConfig = await HassClient.GetConfig().ConfigureAwait(false);

            if (hassConfig.State == "RUNNING")
            {
                return;
            }

            if (!hasRetried)
            {
                _logger.LogInformation("Home Assistant is not ready yet, state: {State}, Waiting ...", hassConfig.State);
            }
            hasRetried = true;
        }
    }

    private async static Task Delay(int timeout, CancellationToken cancelToken)
    {
        try
        {
            // We wait a timeout before reconnecting again
            await Task.Delay(timeout, cancelToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Noop, we expect this to happen
        }
    }
}