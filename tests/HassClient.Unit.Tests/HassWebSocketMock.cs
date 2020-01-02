using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JoySoftware.HomeAssistant.Client;

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
        ServiceCallOk,
        Config,
        ServiceEvent,
        ResultNotOk,
        ResultNotExpected
    }

    public class HassWebSocketFactoryMock : IClientWebSocketFactory
    {
        private readonly List<MockMessageType> _mockMessages;
        public HassWebSocketFactoryMock(List<MockMessageType> mockMessages) => _mockMessages = mockMessages;

        public HassWebSocketMock WebSocketClient { get; private set; }

        public IClientWebSocket New()
        {
            WebSocketClient = new HassWebSocketMock();
            foreach (MockMessageType msg in _mockMessages)
            {
                WebSocketClient.ResponseMessages.Writer.TryWrite(msg);
            }

            return WebSocketClient;
        }
    }

    public class HassWebSocketMock : IClientWebSocket
    {
        private static readonly string mockTestdataPath = Path.Combine(AppContext.BaseDirectory, "Mocks", "testdata");

        private int _currentReadPosition;

        private static byte[] msgAuthRequiredMessage =>
            File.ReadAllBytes(Path.Combine(mockTestdataPath, "auth_required.json"));

        private static byte[] msgAuthOk => File.ReadAllBytes(Path.Combine(mockTestdataPath, "auth_ok.json"));
        private static byte[] msgAuthFail => File.ReadAllBytes(Path.Combine(mockTestdataPath, "auth_notok.json"));
        private static byte[] msgResultSuccess => File.ReadAllBytes(Path.Combine(mockTestdataPath, "result_msg.json"));

        private static byte[] msgResultNotExpected =>
            File.ReadAllBytes(Path.Combine(mockTestdataPath, "result_msg_not_expected.json"));

        private static byte[] msgResultFail =>
            File.ReadAllBytes(Path.Combine(mockTestdataPath, "result_msg_success_false.json"));

        private static byte[] msgNewEvent => File.ReadAllBytes(Path.Combine(mockTestdataPath, "event.json"));
        private static byte[] msgStates => File.ReadAllBytes(Path.Combine(mockTestdataPath, "result_states.json"));
        private static byte[] msgPong => File.ReadAllBytes(Path.Combine(mockTestdataPath, "pong.json"));

        private static byte[] msgServiceCallOk =>
            File.ReadAllBytes(Path.Combine(mockTestdataPath, "service_call_ok.json"));

        private static byte[] msgServiceEvent =>
            File.ReadAllBytes(Path.Combine(mockTestdataPath, "service_event.json"));

        private static byte[] msgConfig => File.ReadAllBytes(Path.Combine(mockTestdataPath, "result_config.json"));

        public bool CloseIsRun { get; set; }
        public Channel<MockMessageType> ResponseMessages { get; } = Channel.CreateBounded<MockMessageType>(10);

        public WebSocketState State { get; set; }

        public WebSocketCloseStatus? CloseStatus { get; set; }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription,
            CancellationToken cancellationToken)
        {
            CloseIsRun = true;
            CloseStatus = closeStatus;
            State = WebSocketState.Closed;

            await Task.Delay(2, cancellationToken).ConfigureAwait(false);
        }

        public async Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription,
            CancellationToken cancellationToken)
        {
            CloseIsRun = true;
            CloseStatus = closeStatus;
            State = WebSocketState.Closed;

            await Task.Delay(2, cancellationToken).ConfigureAwait(false);
        }

        public async Task ConnectAsync(Uri uri, CancellationToken cancel)
        {
            if (uri.AbsoluteUri == "ws://noconnect:9999/")
            {
                State = WebSocketState.Aborted;
            }
            else
            {
                State = WebSocketState.Open;
            }
            // Fake different connectionstatus

            await Task.Delay(2);
        }

        public Task<WebSocketReceiveResult>
            ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            MockMessageType msgToSend =
                await ResponseMessages.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);


            switch (msgToSend)
            {
                case MockMessageType.AuthRequired:
                    return HandleResult(msgAuthRequiredMessage, buffer, msgToSend);

                case MockMessageType.AuthOk:
                    return HandleResult(msgAuthOk, buffer, msgToSend);

                case MockMessageType.AuthFail:
                    return HandleResult(msgAuthFail, buffer, msgToSend);

                case MockMessageType.ResultOk:
                    return HandleResult(msgResultSuccess, buffer, msgToSend);

                case MockMessageType.ResultNotOk:
                    return HandleResult(msgResultFail, buffer, msgToSend);

                case MockMessageType.ResultNotExpected:
                    return HandleResult(msgResultNotExpected, buffer, msgToSend);

                case MockMessageType.NewEvent:
                    return HandleResult(msgNewEvent, buffer, msgToSend);

                case MockMessageType.States:
                    return HandleResult(msgStates, buffer, msgToSend);

                case MockMessageType.Pong:
                    return HandleResult(msgPong, buffer, msgToSend);

                case MockMessageType.ServiceCallOk:
                    return HandleResult(msgServiceCallOk, buffer, msgToSend);

                case MockMessageType.Config:
                    return HandleResult(msgConfig, buffer, msgToSend);

                case MockMessageType.ServiceEvent:
                    return HandleResult(msgServiceEvent, buffer, msgToSend);
            }

            throw new Exception("Expected known mock message type!");
        }

        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken)
        {
            await Task.Delay(2);
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken) => await Task.Delay(2);

        private ValueWebSocketReceiveResult HandleResult(byte[] msg, Memory<byte> buffer, MockMessageType msgType)
        {
            if ((msg.Length - _currentReadPosition) > buffer.Length)
            {
                msg.AsMemory(_currentReadPosition, buffer.Length).CopyTo(buffer);
                _currentReadPosition += buffer.Length;
                // Re-enter the message type in channel cause it is a continuous message
                ResponseMessages.Writer.TryWrite(msgType);
                return new ValueWebSocketReceiveResult(
                    buffer.Length, WebSocketMessageType.Text, false);
            }

            int len = msg.Length - _currentReadPosition;
            msg.AsMemory(_currentReadPosition, len).CopyTo(buffer);

            _currentReadPosition = 0;
            return new ValueWebSocketReceiveResult(
                len, WebSocketMessageType.Text, true);
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

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