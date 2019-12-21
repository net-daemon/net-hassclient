using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    /// <summary>
    /// The interface for ws client
    /// </summary>
    public interface IHassClient
    {
        /// <summary>
        /// Connects to the Home Assistant websocket
        /// </summary>
        /// <param name="url">The url to the websocket. Typically host:8192/api/websocket</param>
        /// <returns>Returns true if connected</returns>
        Task<bool> ConnectAsync(Uri url, string token, bool fetchStatesOnConnect, bool subscribeEvents);

        Task CloseAsync();

        ConcurrentDictionary<string, StateMessage> States { get; }

        //bool SendMessage(MessageBase message);

        //Task<HassMessage> ReadMessageAsync();
    }

    /// <summary>
    /// Hides the internals of websocket connection
    /// to connect, send and receive json messages
    /// This class is threadsafe
    /// </summary>
    public class HassClient : IHassClient, IDisposable
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
        private readonly JsonSerializerOptions defaultSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        /// <summary>
        /// The underlying currently connected socket or null if not connected
        /// </summary>
        private IClientWebSocket? _ws = null;

        private IClientWebSocketFactory? _wsFactory = null;

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
        private readonly Channel<MessageBase> _writeChannel = Channel.CreateBounded<MessageBase>(_DEFAULT_CHANNEL_SIZE);

        /// <summary>
        /// Channel used as a async thread safe way to read resultmessages from the websocket
        /// </summary>
        private Channel<HassMessage> _messageChannel = Channel.CreateBounded<HassMessage>(_DEFAULT_CHANNEL_SIZE);

        /// <summary>
        /// Channel used as a async thread safe way to read messages from the websocket
        /// </summary>
        private Channel<EventMessage> _eventChannel = Channel.CreateBounded<EventMessage>(_DEFAULT_CHANNEL_SIZE);

        public ConcurrentDictionary<string, StateMessage> States { get; } = new ConcurrentDictionary<string, StateMessage>(Environment.ProcessorCount * 2, _DEFAULT_CHANNEL_SIZE);

        private readonly ILogger? _logger = null;
        public HassClient(ILoggerFactory? logFactory = null, IClientWebSocketFactory? wsFactory = null)
        {
            logFactory ??= _getDefaultLoggerFactory;
            wsFactory ??= new ClientWebSocketFactory(); ;

            _logger = logFactory.CreateLogger<HassClient>();
            _wsFactory = wsFactory;
        }

        /// <summary>
        /// The default logger
        /// </summary>
        private static ILoggerFactory _getDefaultLoggerFactory => LoggerFactory.Create(builder =>
                                                                               {
                                                                                   builder
                                                                                       .ClearProviders()
                                                                                       .AddFilter("HassClient.WSClient", LogLevel.Information)
                                                                                       .AddConsole();
                                                                               });

        /// <summary>
        /// Wait and read next message from websocket
        /// </summary>
        /// <returns>Returns a message</returns>
        /// <exception>OperationCanceledException if the operation is canceled.</exception>
        public async Task<EventMessage> ReadEventAsync() => await _eventChannel.Reader.ReadAsync(_cancelSource.Token);

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
        private bool sendMessage(MessageBase message)
        {
            _logger.LogTrace($"Sends message {message.Type}");
            if (message is CommandMessage commandMessage)
            {
                commandMessage.Id = ++_messageId;
                //We save the type of command so we can deserialize the correct message later
                CommandsSent[_messageId] = commandMessage.Type;
            }
            return _writeChannel.Writer.TryWrite(message);
        }

        /// <summary>
        /// Connect to websocket
        /// </summary>
        /// <param name="url">The uri of the websocket</param>
        /// <param name="token">Authtoken from Home Assistant for access</param>
        /// <param name="fetchStatesOnConnect">Reads all states initially, this is the default behaviour</param>
        /// <param name="subscribeEvents">Subscribes to all eventchanges, this is the default behaviour</param>
        /// <returns>Returns true if successfully connected</returns>
        public async Task<bool> ConnectAsync(Uri url, string token,
            bool fetchStatesOnConnect = true, bool subscribeEvents = true)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url), "Expected url to be provided");

            // Check if we already have a websocket running
            if (_ws != null)
            {
                throw new InvalidOperationException("Allready connected to the remote websocket.");
            }

            try
            {
                var ws = _wsFactory?.New()!;
                await ws.ConnectAsync(url, _cancelSource.Token);

                if (ws.State == WebSocketState.Open)
                {
                    // TODO: Set a timeout 
                    _ws = ws;
                    initStates();
                    // Get Response message
                    var result = await _messageChannel.Reader.ReadAsync(_cancelSource.Token);
                    if (result.Type != "auth_required")
                    {
                        await DoNormalClosureOfWebSocket();
                        return false;
                    }
                    sendMessage(new AuthMessage { AccessToken = token });
                    result = await _messageChannel.Reader.ReadAsync(_cancelSource.Token);
                    switch (result.Type)
                    {
                        case "auth_ok":
                            // Initialize the states
                            if (fetchStatesOnConnect)
                            {
                                sendMessage(new GetStatesMessage { });
                                result = await _messageChannel.Reader.ReadAsync(_cancelSource.Token);
                                var wsResult = result?.Result as List<StateMessage>;
                                if (wsResult != null)
                                    foreach (var state in wsResult)
                                    {
                                        States[state.EntityId] = state;
                                    }

                            }
                            if (subscribeEvents)
                            {
                                sendMessage(new SubscribeEventMessage { });
                                result = await _messageChannel.Reader.ReadAsync(_cancelSource.Token);
                                if (result.Type != "result" && result.Success != true)
                                {
                                    _logger.LogError($"Unexpected response from subscribe events ({result.Type}, {result.Success})");
                                    await DoNormalClosureOfWebSocket();
                                    return false;
                                }


                            }

                            _logger.LogTrace($"Connected to websocket ({url})");
                            return true;

                        case "auth_invalid":
                            await DoNormalClosureOfWebSocket();
                            return false;

                        default:
                            _logger.LogError($"Unexpected response ({result.Type})");
                            return false;

                    }

                }
                _logger.LogDebug($"Failed to connect to websocket socket state: {ws.State}");
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to connect to Home Assistant on {url}");
                _logger.LogDebug(e, $"Failed to connect to Home Assistant on {url}");
            }

            return false;

        }

        private void initStates()
        {
            _isValid = true;
            _isClosing = false;

            // Make sure we have new channels so we are not have old messages
            _messageChannel = Channel.CreateBounded<HassMessage>(HassClient._DEFAULT_CHANNEL_SIZE);
            _eventChannel = Channel.CreateBounded<EventMessage>(HassClient._DEFAULT_CHANNEL_SIZE);

            _cancelSource = new CancellationTokenSource();
            _readMessagePumpTask = Task.Run(ReadMessagePump);
            _writeMessagePumpTask = Task.Run(WriteMessagePump);
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

            if (_ws != null &&
                (_ws.State == WebSocketState.CloseReceived ||
                _ws.State == WebSocketState.Open))
            {
                var timeout = new CancellationTokenSource(HassClient._MAX_WAITTIME_SOCKET_CLOSE);
                try
                {
                    // Send close message (some bug n CloseAsync makes we have to do it this way)
                    await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
                    // Wait for readpump finishing when receiving the close message
                    _readMessagePumpTask?.Wait(timeout.Token);
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
                if (_isClosing)
                {
                    return;
                }
            }

            _logger.LogTrace($"Async close websocket");

            // First do websocket close management
            await DoNormalClosureOfWebSocket();
            // Cancel all async stuff
            _cancelSource.Cancel();

            // Wait for read and write tasks to complete max 5 seconds
            if (_readMessagePumpTask != null && _writeMessagePumpTask != null)
            {
                Task.WaitAll(new Task[] { _readMessagePumpTask, _writeMessagePumpTask },
                    HassClient._MAX_WAITTIME_SOCKET_CLOSE, CancellationToken.None);
            }

            _ws?.Dispose();
            _ws = null;

            _isValid = false;
            _isClosing = false;
            _cancelSource = new CancellationTokenSource();

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
            if (!disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                //
                if (disposing)
                {
                    _ws?.Dispose();
                    _cancelSource.Dispose();
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

            if (_ws == null)
            {
                throw new MissingMemberException("_ws is null!");
            }

            var pipe = new Pipe();
            int totalCount = 0;

            // While not canceled and websocket is not closed
            while (!_cancelSource.IsCancellationRequested && !_ws.CloseStatus.HasValue)
            {
                try
                {
                    Memory<byte> memory = pipe.Writer.GetMemory(HassClient._DEFAULT_RECIEIVE_BUFFER_SIZE);

                    ValueWebSocketReceiveResult result = await _ws.ReceiveAsync(memory, _cancelSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close || result.Count == 0)
                    {
                        await CloseAsync();
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
                            HassMessage m = await JsonSerializer.DeserializeAsync<HassMessage>(pipe.Reader.AsStream());
                            await pipe.Reader.CompleteAsync();
                            switch (m.Type)
                            {
                                case "event":
                                    if (m.Event != null)
                                        _eventChannel.Writer.TryWrite(m.Event);
                                    break;
                                case "auth_required":
                                case "auth_ok":
                                case "auth_invalid":
                                case "result":
                                    _messageChannel.Writer.TryWrite(m);
                                    break;
                                default:
                                    _logger.LogDebug($"Unexpected eventtype {m.Type}, discarding message!");
                                    break;
                            }

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
            if (_ws == null)
            {
                throw new MissingMemberException("client_ws is null!");
            }

            while (!_cancelSource.IsCancellationRequested && !_ws.CloseStatus.HasValue)
            {
                try
                {
                    MessageBase nextMessage = await _writeChannel.Reader.ReadAsync(_cancelSource.Token);
                    byte[] result = JsonSerializer.SerializeToUtf8Bytes(nextMessage, nextMessage.GetType(), defaultSerializerOptions);

                    await _ws.SendAsync(result, WebSocketMessageType.Text, true, _cancelSource.Token);
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