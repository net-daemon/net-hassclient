namespace JoySoftware.HomeAssistant.Model;

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
    NotRunning,
    /// <summary>
    ///     Websocket is not connected
    /// </summary>
    Disconnected,
    /// <summary>
    ///     Websocket is connected but status of Home Assstant is not known yet
    /// </summary>
    WebSocketConnected,
}