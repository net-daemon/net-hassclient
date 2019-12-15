using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;


[assembly: InternalsVisibleTo("HassClientIntegrationTests")]
[assembly: InternalsVisibleTo("HassClient.Performance.Tests")]

namespace HassClient
{

    internal interface IWsClient
    {
        Task<bool> ConnectAsync(Uri url);

        Task CloseAsync();

        bool SendMessage(MessageBase message);

        Task<HassMessage> ReadMessageAsync();
    }

    /// <summary>
    /// Hides the internals of websocket connection
    /// to connect, send and receive json messages
    /// This class is threadsafe
    /// </summary>
    internal class WSClient : IWsClient, IDisposable
    {
        /// <summary>
        /// The max time we will wait for the socket to gracefully close
        /// </summary>
        private static readonly int MAX_WAITTIME_SOCKET_CLOSE = 5000; // 5 seconds

        /// <summary>
        /// Default size for channel
        /// </summary>
        private static readonly int CHANNEL_SIZE = 200;

        /// <summary>
        /// Default read buffer size for websockets
        /// </summary>
        public static readonly int RECIEIVE_BUFFER_SIZE = 1024 * 4;

        /// <summary>
        /// Indicates if client is valid
        /// </summary>
        private bool isValid = false;

        /// <summary>
        /// Default Json serialization options, Hass expects intended
        /// </summary>
        private JsonSerializerOptions defaultSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        /// <summary>
        /// The underlying currently connected socket or null if not connected
        /// </summary>
        private ClientWebSocket? client_ws = null;

        /// <summary>
        /// Used to cancel all asyncronus work
        /// </summary>
        private CancellationTokenSource cancelSource = new CancellationTokenSource();

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
        private Channel<MessageBase> writeChannel = Channel.CreateBounded<MessageBase>(WSClient.CHANNEL_SIZE);

        /// <summary>
        /// Channel used as a async thread safe way to read messages from the websocket
        /// </summary>
        private Channel<HassMessage> readChannel = Channel.CreateBounded<HassMessage>(WSClient.CHANNEL_SIZE);

        /// <summary>
        /// Wait and read next message from websocket
        /// </summary>
        /// <returns>Returns a message</returns>
        public async Task<HassMessage> ReadMessageAsync()
        {
            return await readChannel.Reader.ReadAsync();
        }

        /// <summary>
        /// Message id sent in command messages
        /// </summary>
        private int messageId = 0;

        /// <summary>
        /// Thread safe dicitionary that holds information about all command and command id:s
        /// Is used to correclty deserialize the result messages from commands.
        /// </summary>
        /// <typeparam name="int">The message id sen in command message</typeparam>
        /// <typeparam name="string">The message type</typeparam>
        /// <returns></returns>
        public static ConcurrentDictionary<int, string> CommandsSent { get; set; } = new ConcurrentDictionary<int, string>(32, 200);

        /// <summary>
        /// Sends a message through the websocket
        /// </summary>
        /// <param name="message"></param>
        /// <returns>Returns true if succeeded sending message</returns>
        public bool SendMessage(MessageBase message)
        {
            if (message is CommandMessage commandMessage)
            {
                commandMessage.Id = ++this.messageId;
                //We save the type of command so we can deserialize the correct message later
                CommandsSent[this.messageId] = commandMessage.Type;

            }
            return this.writeChannel.Writer.TryWrite(message);
        }

        /// <summary>
        /// Connect to websocket
        /// </summary>
        /// <param name="url">The uri of the websocket</param>
        /// <returns>Returns true if successfully connected</returns>
        public async Task<bool> ConnectAsync(Uri url)
        {
            // Check if we already have a websocket running
            if (this.client_ws != null)
            {
                throw new InvalidOperationException("Can't call connect on connected websocket.");
            }

            var ws = new System.Net.WebSockets.ClientWebSocket();
            await ws.ConnectAsync(url, cancelSource.Token);

            if (ws.State == WebSocketState.Open)
            {
                // Initialize the states
                this.isValid = true;
                this.isClosing = false;
                cancelSource = new CancellationTokenSource();
                this.client_ws = ws;
                this.readMessagePumpTask = Task.Run(ReadMessagePump);
                this.writeMessagePumpTask = Task.Run(WriteMessagePump);
                return true;
            }
            return false;

        }

        /// <summary>
        /// Indicates if we are in the process of closing the socket and cleaning up resources
        /// Avoids recursive states
        /// </summary>
        private bool isClosing = false;

        /// <summary>
        /// Close the websocket gracefully
        /// </summary>
        /// <remarks>
        /// The code waits for the server to return closed state.
        ///
        /// There was problems using the CloseAsync only. It did not properly work as expected
        /// The code is using CloseOutputAsync instead and wait for status closed
        /// </remarks>
        /// <returns></returns>
        private async Task DoNormalClosureOfWebSocket()
        {
            if (this.client_ws != null &&
                (this.client_ws.State == WebSocketState.CloseReceived ||
                this.client_ws.State == WebSocketState.Open ||
                this.client_ws.State == WebSocketState.CloseSent))
            {
                var timeout = new CancellationTokenSource(WSClient.MAX_WAITTIME_SOCKET_CLOSE);
                try
                {
                    // Send close message (some bug n CloseAsync makes we have to do it this way)
                    await this.client_ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
                    // Wait for readpump finishing when receiving the close message
                    this.readMessagePumpTask?.Wait(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    // normal upon task/token cancellation, disregard
                }
            }
        }
        /// <summary>
        /// Closes the websocket
        /// </summary>
        public async Task CloseAsync()
        {
            lock (this)
            {
                if (isClosing) return;
            }
            // First do websocket close management
            await DoNormalClosureOfWebSocket();
            // Cancel all async stuff
            this.cancelSource.Cancel();

            // Wait for read and write tasks to complete max 5 seconds
            if (this.readMessagePumpTask != null && this.writeMessagePumpTask != null)
                Task.WaitAll(new Task[] { this.readMessagePumpTask, this.writeMessagePumpTask },
                    WSClient.MAX_WAITTIME_SOCKET_CLOSE, CancellationToken.None);


            this.client_ws?.Dispose();
            this.client_ws = null;

            this.isValid = false;
            this.isClosing = false;
            this.cancelSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Dispose the WSCLient
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                //
                if (disposing)
                {
                    this.client_ws?.Dispose();
                    this.cancelSource.Dispose();
                }
                disposed = true;
            }
        }

        /// <summary>
        /// A pump that reads incoming messages and put them on the read channel.
        /// </summary>
        private async void ReadMessagePump()
        {
            if (this.client_ws == null)
                throw new MissingMemberException("client_ws is null!");

            var pipe = new Pipe();
            var totalCount = 0;

            while (!cancelSource.IsCancellationRequested && !this.client_ws.CloseStatus.HasValue)
            {
                try
                {
                    Memory<byte> memory = pipe.Writer.GetMemory(WSClient.RECIEIVE_BUFFER_SIZE);

                    var result = await this.client_ws.ReceiveAsync(memory, cancelSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close || result.Count == 0)
                    {
                        // Remote disconnected just leave the readpump
                        return;
                    }
                    pipe.Writer.Advance(result.Count);
                    totalCount += result.Count;

                    if (result.EndOfMessage)
                    {
                        await pipe.Writer.FlushAsync();
                        await pipe.Writer.CompleteAsync();
                        try
                        {
                            var m = await JsonSerializer.DeserializeAsync<HassMessage>(pipe.Reader.AsStream());
                            await pipe.Reader.CompleteAsync();
                            // Todo: check for faults here
                            this.readChannel.Writer.TryWrite(m);
                            pipe = new Pipe();
                            totalCount = 0;

                        }
                        catch (System.Exception e)
                        {
                            // Todo: Log the seralizer error here later but continue receive
                            // messages from the server. Then we can survive the server
                            // Sending bad json messages
                            throw e;
                        }

                    }
                }
                catch (System.OperationCanceledException)
                {
                    // Canceled the thread just leave
                    break;
                }

            }
        }
        private async void WriteMessagePump()
        {
            if (this.client_ws == null)
                throw new MissingMemberException("client_ws is null!");

            while (!cancelSource.IsCancellationRequested && !this.client_ws.CloseStatus.HasValue)
            {
                try
                {
                    var nextMessage = await writeChannel.Reader.ReadAsync(cancelSource.Token);
                    var result = JsonSerializer.SerializeToUtf8Bytes(nextMessage, nextMessage.GetType(), defaultSerializerOptions);

                    await this.client_ws.SendAsync(result, WebSocketMessageType.Text, true, cancelSource.Token);
                }
                catch (System.OperationCanceledException)
                {
                    // Canceled the thread
                    break;
                }
                catch (System.Exception e)
                {
                    // Todo: log
                    throw e;
                }

            }
        }
    }

}