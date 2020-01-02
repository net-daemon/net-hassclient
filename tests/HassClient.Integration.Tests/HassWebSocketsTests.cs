using HassClientIntegrationTests.Mocks;
using JoySoftware.HomeAssistant.Client;
using System;
using System.Net.WebSockets;
using System.Threading;
using Xunit;

namespace HassClientIntegrationTests
{
    /// <summary>
    ///     Used to test unused stuff on the HassWebSocket
    /// </summary>
    public class HassWebSocketsTests : IDisposable

    {
        public HassWebSocketsTests()
        {
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        private readonly HomeAssistantMock _mock;

        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        [Fact]
        public async void TestNotImplementedFeatures()
        {
            using var ws = new HassWebSocket();

            await Assert.ThrowsAsync<NotImplementedException>(() =>
                ws.CloseOutputAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None));
            await Assert.ThrowsAsync<NotImplementedException>(async () =>
                await ws.SendAsync(new ReadOnlyMemory<byte>(), WebSocketMessageType.Text, true,
                    CancellationToken.None));
            await Assert.ThrowsAsync<NotImplementedException>(() =>
                ws.ReceiveAsync(ArraySegment<byte>.Empty, CancellationToken.None));


        }
    }
}