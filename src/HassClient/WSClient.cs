using System;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly : InternalsVisibleTo ("HassClientIntegrationTests")]
namespace HassClient {

    internal interface IWsClient {
        Task<bool> ConnectAsync (Uri url);

        void DisconnectAsync ();
    }

    /// <summary>
    /// Hides the internals of websocket connection
    /// to connect, send and receive json messages
    /// This class is threadsafe
    /// </summary>
    internal class WSClient : IWsClient {
        private ClientWebSocket client_ws = null;
        private CancellationTokenSource cancel_source = null;
        public async Task<bool> ConnectAsync (Uri url) {
            // Check if we already have a websocket running
            if (this.client_ws != null) {
                throw new InvalidOperationException ("Can't call connect on connected websocket.")
            }

            var ws = new System.Net.WebSockets.ClientWebSocket ();
            await ws.ConnectAsync (url, cancel_source.Token);

            if (ws.State == WebSocketState.Open) {
                this.client_ws = ws;
                this.cancel_source = new CancellationTokenSource ();
                return true;
            }
            return false;

        }

        public async void DisconnectAsync () {
            if (this.client_ws == null) {
                throw new InvalidOperationException ("Can't disconnect an disconnected websocket.")
            }
            // Cancel all operations on the socket
            cancel_source.Cancel ();
            if (this.client_ws.State == WebSocketState.Open) {
                // Close the socket
                await this.client_ws.CloseAsync (WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            // Wait for read and write tasks to complete
        }
    }

}