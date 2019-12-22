using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace HassClient.Unit.Tests

{
    public enum MockMessageType
    {
        AuthRequired,
        AuthOk,
        AuthFail,
        ResultOk,
        NewEvent,
        States,
        Pong,
    }

    public class HassWebSocketFactoryMock : IClientWebSocketFactory
    {
        HassWebSocketMock _ws = null;

        public HassWebSocketMock WebSocketClient => _ws;

        private List<MockMessageType> _mockMessages;
        public HassWebSocketFactoryMock(List<MockMessageType> mockMessages)
        {
            _mockMessages = mockMessages;
        }
        public IClientWebSocket New()
        {
            _ws = new HassWebSocketMock();
            foreach (var msg in _mockMessages)
                _ws.ResponseMessages.Writer.TryWrite(msg);
            return _ws;
        }
    }

    public class HassWebSocketMock : IClientWebSocket
    {
        private static readonly string mockTestdataPath = Path.Combine(AppContext.BaseDirectory, "Mocks", "testdata");

        private static byte[] msgAuthRequiredMessage => File.ReadAllBytes(Path.Combine(mockTestdataPath, "auth_required.json"));
        private static byte[] msgAuthOk => File.ReadAllBytes(Path.Combine(mockTestdataPath, "auth_ok.json"));
        private static byte[] msgAuthFail => File.ReadAllBytes(Path.Combine(mockTestdataPath, "auth_notok.json"));
        private static byte[] msgResultSuccess => File.ReadAllBytes(Path.Combine(mockTestdataPath, "result_msg.json"));
        private static byte[] msgNewEvent => File.ReadAllBytes(Path.Combine(mockTestdataPath, "event.json"));
        private static byte[] msgStates => File.ReadAllBytes(Path.Combine(mockTestdataPath, "result_states.json"));
        private static byte[] msgPong => File.ReadAllBytes(Path.Combine(mockTestdataPath, "pong.json"));

        private int _currentMsgIndex = 0;
        private int _currentReadPosition = 0;

        public bool CloseIsRun { get; set; } = false;

        public WebSocketState State { get; set; }

        private readonly Channel<MockMessageType> _responseMessages = Channel.CreateBounded<MockMessageType>(10);
        public Channel<MockMessageType> ResponseMessages => _responseMessages;

        public WebSocketCloseStatus? CloseStatus { get; set; }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) => throw new NotImplementedException();
        public async Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            CloseIsRun = true;
            CloseStatus = WebSocketCloseStatus.NormalClosure;
            State = WebSocketState.Closed;
            await Task.Delay(2);
        }
        public async Task ConnectAsync(Uri uri, CancellationToken cancel)
        {
            State = WebSocketState.Open;
            await Task.Delay(2);
        }
        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotImplementedException();

        private async ValueTask<ValueWebSocketReceiveResult> recres(byte[] msg, Memory<byte> buffer, MockMessageType msgType)
        {
            if ((msg.Length - _currentReadPosition) > buffer.Length)
            {
                msg.AsMemory<byte>(_currentReadPosition, buffer.Length).CopyTo(buffer);
                _currentReadPosition += buffer.Length;
                // Re-enter the message type in channel cause it is a continuous message
                _responseMessages.Writer.TryWrite(msgType);
                return new ValueWebSocketReceiveResult(
                         buffer.Length, WebSocketMessageType.Text, false);
            }
            else
            {
                var len = msg.Length - _currentReadPosition;
                msg.AsMemory<byte>(_currentReadPosition, len).CopyTo(buffer);

                _currentReadPosition = 0;
                _currentMsgIndex++; // Full message, add next index
                return new ValueWebSocketReceiveResult(
                             len, WebSocketMessageType.Text, true);
            }

        }
        public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var msgToSend = await _responseMessages.Reader.ReadAsync(cancellationToken);


            switch (msgToSend)
            {
                case MockMessageType.AuthRequired:
                    return await recres(msgAuthRequiredMessage, buffer, msgToSend);

                case MockMessageType.AuthOk:
                    return await recres(msgAuthOk, buffer, msgToSend);

                case MockMessageType.AuthFail:
                    return await recres(msgAuthFail, buffer, msgToSend);

                case MockMessageType.ResultOk:
                    return await recres(msgResultSuccess, buffer, msgToSend);

                case MockMessageType.NewEvent:
                    return await recres(msgNewEvent, buffer, msgToSend);

                case MockMessageType.States:
                    return await recres(msgStates, buffer, msgToSend);

                case MockMessageType.Pong:
                    return await recres(msgPong, buffer, msgToSend);

            }

            throw new Exception("Expected known mock message type!");

        }
        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {

            await Task.Delay(2);
        }
        public async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            await Task.Delay(2);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls
        private List<MockMessageType> _mockMessages;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~HassWebSocketMock()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

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
