using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace HassClient
{
    /// <summary>
    /// Interface for Home Assistant cliuent
    /// </summary>
    public interface IHassClient
    {
        Task<bool> ConnectAsync(Uri uri);
        Task CloseAsync();
    }

    public class HassClient : IHassClient
    {
        private HassClient() { }

        private readonly IWsClient? _wsClient = null;
        private readonly ILogger? _logger = null;

        public HassClient(IWsClient wsClient, ILoggerFactory? factory = null)
        {
            // Logger setup
            factory ??= DefaultLoggerFactory;
            _logger = factory.CreateLogger<WSClient>();

            _wsClient = wsClient ?? new WSClient(factory);

        }

        private static ILoggerFactory DefaultLoggerFactory => LoggerFactory.Create(builder =>
                           {
                               builder
                                   .ClearProviders()
                                   .AddFilter("HassClient.WSClient", LogLevel.Information)
                                   .AddConsole();
                           });

        public async Task<bool> ConnectAsync(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri), $"{ nameof(uri) } is null");
            }

            try
            {
                return await _wsClient?.ConnectAsync(uri)!;
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to connect to Home Assistant on {uri}");
                _logger.LogDebug(e, $"Failed to connect to Home Assistant on {uri}");
                return false;
            }

        }

        public async Task CloseAsync() => await _wsClient?.CloseAsync()!;
        public async Task<HassMessage> ReadMessageAsync() => await _wsClient?.ReadMessageAsync()!;
    }
}