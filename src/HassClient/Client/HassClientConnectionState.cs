namespace JoySoftware.HomeAssistant.Client;

/// <summary>
///     Hass the state that is returned during connection to Home Assistant from HassClient
/// </summary>
public class HassClientConnectionState
{
    private readonly Subject<ConnectionStatus> _connectionStatusSubject;
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;

    public HassClientConnectionState(CancellationToken? cancelToken, Subject<ConnectionStatus>? connectionStatusSubject)
    {
        DisconnectToken = cancelToken ?? new();
        _connectionStatusSubject = connectionStatusSubject ?? new();
        // Internal subscription to set the status
        ConnectionStateObservable.Subscribe(s => _connectionStatus = s);
    }

    public ConnectionStatus ConnectionStatus => _connectionStatus;

    /// <summary>
    ///     Cancellation token that can be used to signal disconnections
    /// </summary>
    public CancellationToken DisconnectToken { get; }

    /// <summary>
    ///     Expose changes in connection state as observable events
    /// </summary>
    public IObservable<ConnectionStatus> ConnectionStateObservable => _connectionStatusSubject;

    /// <summary>
    ///     To be compatible with the interface that used to return a boolean
    /// </summary>
    /// <param name="m">The ClientHassSate object</param>
    [SuppressMessage("", "CA2225")]
    public static implicit operator bool(HassClientConnectionState hccs)
    {
        _ = hccs ?? throw new NullReferenceException(nameof(hccs));
        return hccs.ConnectionStatus != ConnectionStatus.Disconnected;
    }
}

/// <summary>
///     Status of the connection to Home Assistant
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    ///     Home Assistant is connected and ready for commands and events
    /// </summary>
    Connected,
    /// <summary>
    ///     Websocket is connected but Home Assistant is not running yet
    /// </summary>
    NotReady,
    /// <summary>
    ///     HassClient is not connected to HomeAssistant
    /// </summary>
    Disconnected,
}
