using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
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
        private static readonly int CHANNEL_WRITE_SIZE = 100;
        public static readonly int RECIEIVE_BUFFER_SIZE = 1024 * 4;
        private ClientWebSocket client_ws = null;
        private CancellationTokenSource cancel_source = null;
        private Task readTask = null;

        private Channel<byte[]> writeChannel = null;
        private Task writeTask = null;
        // System.IO.Pipes.
        private Pipe pipe = new Pipe ();

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
                this.writeChannel = Channel.CreateBounded<byte[]> (WSClient.CHANNEL_WRITE_SIZE);
                this.readTask = Task.Run (ReadMessagePump);
                this.writeTask = Task.Run (WriteMessagePump);
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
            // Wait for read and write tasks to complete max 5 seconds
            Task.WaitAll (new Task[] { this.readTask, this.writeTask }, 5000, CancellationToken.None);
        }

        private async void ReadMessagePump () {
            // using IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent (WSClient.RECIEIVE_BUFFER_SIZE);
            // byte[] buffer = new byte[WSClient.RECIEIVE_BUFFER_SIZE];
            // var array = new ArraySegment<byte> (buffer);
            var writer = pipe.Writer;
            var reader = pipe.Reader;

            Memory<byte> memory = writer.GetMemory (WSClient.RECIEIVE_BUFFER_SIZE);
            var result = await this.client_ws.ReceiveAsync (memory, CancellationToken.None);

            while (!this.client_ws.CloseStatus.HasValue) {
                writer.Advance (result.Count);
                if (result.EndOfMessage) {

                    await writer.FlushAsync ();
                    await writer.CompleteAsync ();
                    Message m = await JsonSerializer.DeserializeAsync<Message> (reader.AsStream ());

                }
                result = await this.client_ws.ReceiveAsync (memory, CancellationToken.None);
            }
        }
        private async void WriteMessagePump () {
            return;
        }
    }

}