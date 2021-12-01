

using JoySoftware.HomeAssistant.Client;

namespace JoySoftware.HomeAssistant.Runner;

/// <summary>
///     Maintains a connection to HomeAssistant and handles connectionstate
/// </summary>
public interface IHassClientRunner
{
    /// <summary>
    ///     Maintain connection to Home Assistant and process messages
    /// </summary>
    /// <param name="host">The host or ip address of Home Assistant</param>
    /// <param name="port">The port of Home Assistant, typically 8123 or 80</param>
    /// <param name="ssl">Set to true if Home Assistant using ssl (recommended secure setup for Home Assistant)</param>
    /// <param name="cancelToken">AuthToken from Home Assistant for access</param>
    Task Run(string host, short port, bool ssl, string hassToken, CancellationToken cancelToken);

    /// <summary>
    ///     The running instance of HassClient
    /// </summary>
    IHassClient HassClient { get; }
}