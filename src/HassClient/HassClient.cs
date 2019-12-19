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
    }

    public class HassClient : IHassClient {
        private HassClient() { }

        IWsClient? _wsClient = null;
        public HassClient(IWsClient wsClient)
        {
            _wsClient = wsClient;
        }

        public async Task<bool> ConnectAsync(Uri uri)
        {
            if (uri == null || _wsClient == null)
                throw new ArgumentNullException("uri", "_wsClient or uri is null");

            return await _wsClient.ConnectAsync(uri);
        }

        public async Task CloseAsync()
        {
            await _wsClient?.CloseAsync();
        }
    }
}