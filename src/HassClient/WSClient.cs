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
[assembly : InternalsVisibleTo ("HassClient.Performance.Tests")]

namespace HassClient {

    internal interface IWsClient {
        Task<bool> ConnectAsync (Uri url);

        Task DisconnectAsync ();

        Task SendMessage (MessageBase message);
    }

    /// <summary>
    /// Hides the internals of websocket connection
    /// to connect, send and receive json messages
    /// This class is threadsafe
    /// </summary>
    internal class WSClient : IWsClient {
        private static readonly int CHANNEL_WRITE_SIZE = 100;
        public static readonly int RECIEIVE_BUFFER_SIZE = 1024 * 4;

        private JsonSerializerOptions serializeOptions = new JsonSerializerOptions {
            WriteIndented = true
        };

        private ClientWebSocket client_ws = null;
        private CancellationTokenSource cancel_source = new CancellationTokenSource ();
        private Task readTask = null;

        private Channel<MessageBase> writeChannel = null;
        private Channel<MessageBase> readChannel = null;
        private Task writeTask = null;
        // System.IO.Pipes.

        public async Task<MessageBase> WaitForMessage () {
            await readChannel.Reader.WaitToReadAsync ();
            return await readChannel.Reader.ReadAsync ();
        }

        public async Task SendMessage (MessageBase message) {
            await this.writeChannel.Writer.WriteAsync (message);
        }
        public async Task<bool> ConnectAsync (Uri url) {
            // Check if we already have a websocket running
            if (this.client_ws != null) {
                throw new InvalidOperationException ("Can't call connect on connected websocket.");
            }

            var ws = new System.Net.WebSockets.ClientWebSocket ();
            await ws.ConnectAsync (url, cancel_source.Token);

            if (ws.State == WebSocketState.Open) {
                this.client_ws = ws;
                this.writeChannel = Channel.CreateBounded<MessageBase> (WSClient.CHANNEL_WRITE_SIZE);
                this.readChannel = Channel.CreateBounded<MessageBase> (WSClient.CHANNEL_WRITE_SIZE);
                this.readTask = Task.Run (ReadMessagePump);
                this.writeTask = Task.Run (WriteMessagePump);
                return true;
            }
            return false;

        }

        public async Task DisconnectAsync () {
            if (this.client_ws == null) {
                throw new InvalidOperationException ("Can't disconnect an disconnected websocket.");
            }

            if (this.client_ws.State == WebSocketState.Open) {
                // Close the socket
                await this.client_ws.CloseAsync (WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            // Cancel all operations on the socket
            cancel_source.Cancel ();
            // Wait for read and write tasks to complete max 5 seconds
            Task.WaitAll (new Task[] { this.readTask, this.writeTask }, 5000, CancellationToken.None);
        }

        private async void ReadMessagePump () {

            while (!cancel_source.IsCancellationRequested && !this.client_ws.CloseStatus.HasValue) {
                var pipe = new Pipe ();
                var writer = pipe.Writer;
                var reader = pipe.Reader;

                Memory<byte> memory = writer.GetMemory (WSClient.RECIEIVE_BUFFER_SIZE);
                var result = await this.client_ws.ReceiveAsync (memory, cancel_source.Token);
                if (result.Count == 0) break; // We canceled.
                writer.Advance (result.Count);
                if (result.EndOfMessage) {

                    await writer.FlushAsync ();
                    await writer.CompleteAsync ();
                    MessageBase m = null;
                    try {
                        m = await JsonSerializer.DeserializeAsync<MessageBase> (reader.AsStream ());
                        await reader.CompleteAsync ();
                        await this.readChannel.Writer.WriteAsync (m);

                    } catch (System.Exception e) {

                        throw e;
                    }

                }

            }
        }
        private async void WriteMessagePump () {
            while (!cancel_source.IsCancellationRequested && !this.client_ws.CloseStatus.HasValue) {
                try {
                    var nextMessage = await writeChannel.Reader.ReadAsync (cancel_source.Token);
                    var result = JsonSerializer.SerializeToUtf8Bytes (nextMessage, nextMessage.GetType (), serializeOptions);

                    await this.client_ws.SendAsync (result, WebSocketMessageType.Text, true, cancel_source.Token);
                } catch (System.OperationCanceledException) {
                    // Canceled the thread
                    break;
                } catch (System.Exception e) {
                    // Todo log
                    throw e;
                }

            }
        }
    }
    // while ( return;
    // }
}