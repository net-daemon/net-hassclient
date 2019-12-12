using System;
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

        bool SendMessage (MessageBase message);
    }

    /// <summary>
    /// Hides the internals of websocket connection
    /// to connect, send and receive json messages
    /// This class is threadsafe
    /// </summary>
    internal class WSClient : IWsClient {
        /// <summary>
        /// Default size for channel
        /// </summary>
        private static readonly int CHANNEL_SIZE = 200;

        /// <summary>
        /// Default read buffer size for websockets
        /// </summary>
        public static readonly int RECIEIVE_BUFFER_SIZE = 1024 * 4;

        /// <summary>
        /// Default Json serialization options, Hass expects intended
        /// </summary>
        private JsonSerializerOptions defaultSerializerOptions = new JsonSerializerOptions {
            WriteIndented = true
        };

        /// <summary>
        /// The underlying currently connected socket or null if not connected
        /// </summary>
        private ClientWebSocket? client_ws = null;

        /// <summary>
        /// Used to cancel all asyncronus work
        /// </summary>
        private CancellationTokenSource cancelSource = new CancellationTokenSource ();

        /// <summary>
        /// Async task to read all incoming messages
        /// </summary>
        private Task? readMessagePumpTask = null;

        /// <summary>
        /// Async task to write messages
        /// </summary>
        private Task? writeMessagePumpTask = null;

        /// <summary>
        /// Channel used as a async thread safe way to wite messages to the websocket
        /// </summary>
        private Channel<MessageBase> writeChannel = Channel.CreateBounded<MessageBase> (WSClient.CHANNEL_SIZE);

        /// <summary>
        /// Channel used as a async thread safe way to read messages from the websocket
        /// </summary>
        private Channel<HassMessage> readChannel = Channel.CreateBounded<HassMessage> (WSClient.CHANNEL_SIZE);

        /// <summary>
        /// Wait and read next message from websocket
        /// </summary>
        /// <returns>Returns a message</returns>
        public async Task<HassMessage> ReadMessageAsync () {
            await readChannel.Reader.WaitToReadAsync ();
            return await readChannel.Reader.ReadAsync ();
        }

        /// <summary>
        /// Message id sent in command messages
        /// </summary>
        private int messageId = 0;
        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool SendMessage (MessageBase message) {
            if (message is IMessageHasId messageWithId) {
                messageWithId.Id = ++this.messageId;
            }

            return this.writeChannel.Writer.TryWrite (message);
        }
        public async Task<bool> ConnectAsync (Uri url) {
            // Check if we already have a websocket running
            if (this.client_ws != null) {
                throw new InvalidOperationException ("Can't call connect on connected websocket.");
            }

            var ws = new System.Net.WebSockets.ClientWebSocket ();
            await ws.ConnectAsync (url, cancelSource.Token);

            if (ws.State == WebSocketState.Open) {
                this.client_ws = ws;
                this.readMessagePumpTask = Task.Run (ReadMessagePump);
                this.writeMessagePumpTask = Task.Run (WriteMessagePump);
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
            cancelSource.Cancel ();
            // Wait for read and write tasks to complete max 5 seconds
            if (this.readMessagePumpTask != null && this.writeMessagePumpTask != null)
                Task.WaitAll (new Task[] { this.readMessagePumpTask, this.writeMessagePumpTask }, 5000, CancellationToken.None);
        }

        private async void ReadMessagePump () {
            if (this.client_ws == null)
                throw new MissingMemberException ("client_ws is null!");

            while (!cancelSource.IsCancellationRequested && !this.client_ws.CloseStatus.HasValue) {
                var pipe = new Pipe ();
                var writer = pipe.Writer;
                var reader = pipe.Reader;

                Memory<byte> memory = writer.GetMemory (WSClient.RECIEIVE_BUFFER_SIZE);
                var result = await this.client_ws.ReceiveAsync (memory, cancelSource.Token);
                if (result.Count == 0) break; // We canceled.
                writer.Advance (result.Count);
                if (result.EndOfMessage) {

                    await writer.FlushAsync ();
                    await writer.CompleteAsync ();
                    try {
                        var m = await JsonSerializer.DeserializeAsync<HassMessage> (reader.AsStream ());
                        await reader.CompleteAsync ();
                        // Todo: check for faults here
                        this.readChannel.Writer.TryWrite (m);

                    } catch (System.Exception e) {

                        throw e;
                    }

                }

            }
        }
        private async void WriteMessagePump () {
            if (this.client_ws == null)
                throw new MissingMemberException ("client_ws is null!");

            while (!cancelSource.IsCancellationRequested && !this.client_ws.CloseStatus.HasValue) {
                try {
                    var nextMessage = await writeChannel.Reader.ReadAsync (cancelSource.Token);
                    var result = JsonSerializer.SerializeToUtf8Bytes (nextMessage, nextMessage.GetType (), defaultSerializerOptions);

                    await this.client_ws.SendAsync (result, WebSocketMessageType.Text, true, cancelSource.Token);
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

}