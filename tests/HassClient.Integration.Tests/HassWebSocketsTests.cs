using System;
using System.Net.WebSockets;
using System.Threading;
using JoySoftware.HomeAssistant.Client;
using Xunit;

namespace HassClientIntegrationTests
{
    /// <summary>
    ///     Used to test unused stuff on the HassWebSocket
    /// </summary>
    public class HassWebSocketsTests

    {
        [Fact]
        public async void TestNotImplementedFeatures()
        {
            using var ws = new HassWebSocket();

            await Assert.ThrowsAsync<NotImplementedException>(async () =>
                await ws.SendAsync(new ReadOnlyMemory<byte>(), WebSocketMessageType.Text, true,
                    CancellationToken.None));
            await Assert.ThrowsAsync<NotImplementedException>(() =>
                ws.ReceiveAsync(ArraySegment<byte>.Empty, CancellationToken.None));
        }
    }
}