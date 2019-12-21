using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
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
    }

    public class HassWebSocketFactoryMock : IClientWebSocketFactory
    {
        private List<MockMessageType> _mockMessages;
        public HassWebSocketFactoryMock(List<MockMessageType> mockMessages)
        {
            _mockMessages = mockMessages;
        }
        public IClientWebSocket New() => new HassWebSocketMock(_mockMessages);
    }

    public class HassWebSocketMock : IClientWebSocket
    {
        private static readonly string mockTestdataPath = Path.Combine(AppContext.BaseDirectory, "Mocks", "testdata");

        private static byte[] msgAuthRequiredMessage => File.ReadAllBytes(Path.Combine(mockTestdataPath, "auth_required.json"));
        private static byte[] msgAuthOk => File.ReadAllBytes(Path.Combine(mockTestdataPath, "auth_ok.json"));
        private static byte[] msgAuthFail => File.ReadAllBytes(Path.Combine(mockTestdataPath, "auth_notok.json"));

        private static byte[] msgResultSuccess => File.ReadAllBytes(Path.Combine(mockTestdataPath, "result_msg.json"));
        private static byte[] msgNewEvent => File.ReadAllBytes(Path.Combine(mockTestdataPath, "event.json"));


        private int _currentMsgIndex = 0;

        public HassWebSocketMock(List<MockMessageType> mockMessages)
        {
            _mockMessages = mockMessages;
        }

        public WebSocketState State { get; set; }

        public WebSocketCloseStatus? CloseStatus { get; set; }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) => throw new NotImplementedException();
        public async Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
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

        private ValueTask<ValueWebSocketReceiveResult> recres(byte[] msg, ref Memory<byte> buffer, bool lastMessage = true)
        {
            msg.CopyTo(buffer);
            return new ValueTask<ValueWebSocketReceiveResult>(new ValueWebSocketReceiveResult(
                         msg.Length, WebSocketMessageType.Text, lastMessage));
        }
        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_currentMsgIndex >= _mockMessages.Count)
            {
                Task.Delay(1000, cancellationToken);
                throw new OperationCanceledException(cancellationToken);
            }

            var msgToSend = _mockMessages[_currentMsgIndex++];

            switch (msgToSend)
            {
                case MockMessageType.AuthRequired:
                    return recres(msgAuthRequiredMessage, ref buffer);

                case MockMessageType.AuthOk:
                    return recres(msgAuthOk, ref buffer);

                case MockMessageType.AuthFail:
                    return recres(msgAuthFail, ref buffer);

                case MockMessageType.ResultOk:
                    return recres(msgResultSuccess, ref buffer);

                case MockMessageType.NewEvent:
                    return recres(msgNewEvent, ref buffer);

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
