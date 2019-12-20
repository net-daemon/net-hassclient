using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace HassClient
{
    /// <summary>
    /// Interface so we can test without the socket layer
    /// </summary>
    public interface IClientWebSocket : IDisposable
    {
        WebSocketState State { get; }
        WebSocketCloseStatus? CloseStatus { get; }

        Task ConnectAsync(Uri uri, CancellationToken cancel);
        Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);
        Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);

        Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);
        ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);


        Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
        ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    }
    public class HassWebSocket : IClientWebSocket
    {
        private readonly ClientWebSocket _ws = new System.Net.WebSockets.ClientWebSocket();

        public WebSocketState State => _ws.State;

        public WebSocketCloseStatus? CloseStatus => _ws.CloseStatus;

        public async Task ConnectAsync(Uri uri, CancellationToken cancel)
        {
            await _ws.ConnectAsync(uri, cancel);
        }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            await _ws.CloseAsync(closeStatus, statusDescription, cancellationToken);
        }

        public async Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            await _ws.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }
        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            await _ws.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }
        public async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            await _ws.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            return await _ws.ReceiveAsync(buffer, cancellationToken);
        }

        public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            return await _ws.ReceiveAsync(buffer, cancellationToken);
        }

        public void Dispose() => _ws.Dispose();
    }
}