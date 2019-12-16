using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


[assembly: InternalsVisibleTo("HassClientIntegrationTests")]
[assembly: InternalsVisibleTo("HassClient.Performance.Tests")]

namespace HassClient
{

    /// <summary>
    /// The interface for ws client
    /// </summary>
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
        private static readonly int _MAX_WAITTIME_SOCKET_CLOSE = 5000; // 5 seconds

        /// <summary>
        /// Default size for channel
        /// </summary>
        private static readonly int _DEFAULT_CHANNEL_SIZE = 200;

        /// <summary>
        /// Default read buffer size for websockets
        /// </summary>
        private static readonly int _DEFAULT_RECIEIVE_BUFFER_SIZE = 1024 * 4;

        /// <summary>
        /// Indicates if client is valid
        /// </summary>
        private bool _isValid = false;

        /// <summary>
        /// Default Json serialization options, Hass expects intended
        /// </summary>
        private JsonSerializerOptions defaultSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        /// <summary>
        /// The underlying currently connected socket or null if not connected
        /// </summary>
        private ClientWebSocket? _ws = null;

        /// <summary>
        /// Used to cancel all asyncronus work
        /// </summary>
        private CancellationTokenSource _cancelSource = new CancellationTokenSource();

        /// <summary>
        /// Async task to read all incoming messages
        /// </summary>
        private Task? _readMessagePumpTask = null;

        /// <summary>
        /// Async task to write messages
        /// </summary>
        private Task? _writeMessagePumpTask = null;

        /// <summary>
        /// Channel used as a async thread safe way to wite messages to the websocket
        /// </summary>
        private Channel<MessageBase> _writeChannel = Channel.CreateBounded<MessageBase>(WSClient._DEFAULT_CHANNEL_SIZE);

        /// <summary>
        /// Channel used as a async thread safe way to read messages from the websocket
        /// </summary>
        private Channel<HassMessage> _readChannel = Channel.CreateBounded<HassMessage>(WSClient._DEFAULT_CHANNEL_SIZE);

        private ILogger? _logger = null;
        public WSClient(ILoggerFactory? factory = null)
        {
            if (factory == null)
            {
                factory = LoggerFactory.Create(builder =>
                {
                    builder
                        .ClearProviders()
                        .AddFilter("HassClient.WSClient", LogLevel.Trace)
                        .AddConsole();
                });

            }
            _logger = factory?.CreateLogger<WSClient>();

        }

        /// <summary>
        /// Wait and read next message from websocket
        /// </summary>
        /// <returns>Returns a message</returns>
        /// <exception>OperationCanceledException if the operation is canceled.</exception>
        public async Task<HassMessage> ReadMessageAsync()
        {
            return await _readChannel.Reader.ReadAsync(this._cancelSource.Token);
        }

        /// <summary>
        /// Message id sent in command messages
        /// </summary>
        private int _messageId = 0;

        /// <summary>
        /// Thread safe dicitionary that holds information about all command and command id:s
        /// Is used to correclty deserialize the result messages from commands.
        /// </summary>
        /// <typeparam name="int">The message id sen in command message</typeparam>
        /// <typeparam name="string">The message type</typeparam>
        public static ConcurrentDictionary<int, string> CommandsSent { get; set; } = new ConcurrentDictionary<int, string>(32, 200);

        /// <summary>
        /// Sends a message through the websocket
        /// </summary>
        /// <param name="message"></param>
        /// <returns>Returns true if succeeded sending message</returns>
        public bool SendMessage(MessageBase message)
        {
            _logger.LogTrace($"Sends message {message.Type}");
            if (message is CommandMessage commandMessage)
            {
                commandMessage.Id = ++this._messageId;
                //We save the type of command so we can deserialize the correct message later
                CommandsSent[this._messageId] = commandMessage.Type;

            }
            return this._writeChannel.Writer.TryWrite(message);
        }

        /// <summary>
        /// Connect to websocket
        /// </summary>
        /// <param name="url">The uri of the websocket</param>
        /// <returns>Returns true if successfully connected</returns>
        public async Task<bool> ConnectAsync(Uri url)
        {
            // Check if we already have a websocket running
            if (this._ws != null)
            {
                throw new InvalidOperationException("Allready connected to the remote websocket.");
            }

            var ws = new System.Net.WebSockets.ClientWebSocket();
            await ws.ConnectAsync(url, _cancelSource.Token);

            if (ws.State == WebSocketState.Open)
            {
                // Initialize the states
                this._isValid = true;
                this._isClosing = false;
                _cancelSource = new CancellationTokenSource();
                this._ws = ws;
                this._readMessagePumpTask = Task.Run(ReadMessagePump);
                this._writeMessagePumpTask = Task.Run(WriteMessagePump);
                _logger.LogTrace($"Connected to websocket ({url})");
                return true;
            }
            _logger.LogTrace($"Failed to connect to websocket socket state: {ws.State}");

            return false;

        }

        /// <summary>
        /// Indicates if we are in the process of closing the socket and cleaning up resources
        /// Avoids recursive states
        /// </summary>
        private bool _isClosing = false;

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
            _logger.LogTrace($"Do normal close of websocket");

            if (this._ws != null &&
                (this._ws.State == WebSocketState.CloseReceived ||
                this._ws.State == WebSocketState.Open ||
                this._ws.State == WebSocketState.CloseSent))
            {
                var timeout = new CancellationTokenSource(WSClient._MAX_WAITTIME_SOCKET_CLOSE);
                try
                {
                    // Send close message (some bug n CloseAsync makes we have to do it this way)
                    await this._ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
                    // Wait for readpump finishing when receiving the close message
                    this._readMessagePumpTask?.Wait(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    // normal upon task/token cancellation, disregard
                    _logger.LogTrace($"Close operations took more than 5 seconds.. closing hard!");
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
                if (_isClosing) return;
            }

            _logger.LogTrace($"Async close websocket");

            // First do websocket close management
            await DoNormalClosureOfWebSocket();
            // Cancel all async stuff
            this._cancelSource.Cancel();

            // Wait for read and write tasks to complete max 5 seconds
            if (this._readMessagePumpTask != null && this._writeMessagePumpTask != null)
                Task.WaitAll(new Task[] { this._readMessagePumpTask, this._writeMessagePumpTask },
                    WSClient._MAX_WAITTIME_SOCKET_CLOSE, CancellationToken.None);


            this._ws?.Dispose();
            this._ws = null;

            this._isValid = false;
            this._isClosing = false;
            this._cancelSource = new CancellationTokenSource();

            _logger.LogTrace($"Async close websocket done");
        }

        /// <summary>
        /// Dispose the WSCLient
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
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
                    this._ws?.Dispose();
                    this._cancelSource.Dispose();
                }
                disposed = true;
            }
        }

        /// <summary>
        /// A pump that reads incoming messages and put them on the read channel.
        /// </summary>
        private async void ReadMessagePump()
        {
            _logger?.LogTrace($"Start ReadMessagePump");

            if (this._ws == null)
                throw new MissingMemberException("_ws is null!");

            var pipe = new Pipe();
            var totalCount = 0;

            // While not canceled and websocket is not closed
            while (!_cancelSource.IsCancellationRequested && !this._ws.CloseStatus.HasValue)
            {
                try
                {
                    Memory<byte> memory = pipe.Writer.GetMemory(WSClient._DEFAULT_RECIEIVE_BUFFER_SIZE);

                    var result = await this._ws.ReceiveAsync(memory, _cancelSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close || result.Count == 0)
                    {
                        await this.CloseAsync();
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
                            this._readChannel.Writer.TryWrite(m);
                            pipe = new Pipe();
                            totalCount = 0;

                        }
                        catch (System.Exception e)
                        {
                            // Todo: Log the seralizer error here later but continue receive
                            // messages from the server. Then we can survive the server
                            // Sending bad json messages
                            _logger?.LogDebug(e, "Error deserialize json response");
                            // Make sure we put a small delay incase we have severe error so the loop
                            // doesnt kill the server
                            await Task.Delay(20);
                        }

                    }
                }
                catch (System.OperationCanceledException)
                {
                    // Canceled the thread just leave
                    break;
                }

            }
            _logger?.LogTrace($"Exit ReadMessagePump");
        }
        private async void WriteMessagePump()
        {
            _logger?.LogTrace($"Start WriteMessagePump");
            if (this._ws == null)
                throw new MissingMemberException("client_ws is null!");

            while (!_cancelSource.IsCancellationRequested && !this._ws.CloseStatus.HasValue)
            {
                try
                {
                    var nextMessage = await _writeChannel.Reader.ReadAsync(_cancelSource.Token);
                    var result = JsonSerializer.SerializeToUtf8Bytes(nextMessage, nextMessage.GetType(), defaultSerializerOptions);

                    await this._ws.SendAsync(result, WebSocketMessageType.Text, true, _cancelSource.Token);
                }
                catch (System.OperationCanceledException)
                {
                    // Canceled the thread
                    break;
                }
                catch (System.Exception e)
                {
                    _logger?.LogWarning($"Exit WriteMessagePump");
                    await Task.Delay(20); // Incase we are looping add a delay
                    throw e;
                }

            }
            _logger?.LogTrace($"Exit WriteMessagePump");
        }

    }

}