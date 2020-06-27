using System;
using System.Net.Security;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace JoySoftware.HomeAssistant.Client
{
    /// <summary>
    ///     Factory for Client Websocket. Implement to use for mockups
    /// </summary>
    public interface IClientWebSocketFactory
    {
        IClientWebSocket New();
    }

    /// <summary>
    ///     Interface so we can test without the socket layer
    /// </summary>
    /// <remarks>Defaul implements the System.Net.WebSockets.ClientWebSocket.</remarks>
    public interface IClientWebSocket : IDisposable
    {
        WebSocketState State { get; }
        WebSocketCloseStatus? CloseStatus { get; }

        Task ConnectAsync(Uri uri, CancellationToken cancel);

        Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription,
            CancellationToken cancellationToken);

        Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription,
            CancellationToken cancellationToken);

        Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken);

        ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken);

        Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
        ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    }

    internal class ClientWebSocketFactory : IClientWebSocketFactory
    {
        public IClientWebSocket New() => new HassWebSocket();
    }

    internal class HassWebSocket : IClientWebSocket
    {
        private readonly ClientWebSocket _ws;

        public WebSocketState State => _ws.State;

        public WebSocketCloseStatus? CloseStatus => _ws.CloseStatus;

        public Task ConnectAsync(Uri uri, CancellationToken cancel) => _ws.ConnectAsync(uri, cancel);

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription,
            CancellationToken cancellationToken) =>
            _ws.CloseAsync(closeStatus, statusDescription, cancellationToken);

        public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription,
            CancellationToken cancellationToken) =>
            _ws.CloseAsync(closeStatus, statusDescription, cancellationToken);

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken) =>
            _ws.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

        public async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken) =>
            await Task.FromException(new NotImplementedException());

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,
            CancellationToken cancellationToken) =>
             Task.FromException<WebSocketReceiveResult>(new NotImplementedException());

        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer,
            CancellationToken cancellationToken) => _ws.ReceiveAsync(buffer, cancellationToken);

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        /// <summary>
        ///    Default constructor
        /// </summary>
        public HassWebSocket()
        {
            _ws = new ClientWebSocket();

            var bypassCertificateErrorsForHash = Environment.GetEnvironmentVariable("HASSCLIENT_BYPASS_CERT_ERR");

            if (bypassCertificateErrorsForHash is object)
            {
                _ws.Options.RemoteCertificateValidationCallback = (message, cert, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)
                    {
                        return true;   //Is valid
                    }

                    if (cert.GetCertHashString() == bypassCertificateErrorsForHash)
                    {
                        return true;
                    }
                    return false;
                };
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _ws.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion
    }
}