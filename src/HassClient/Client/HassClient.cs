using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("HassClientIntegrationTests")]
[assembly: InternalsVisibleTo("HassClient.Performance.Tests")]
[assembly: InternalsVisibleTo("HassClient.Unit.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace JoySoftware.HomeAssistant.Client
{
    /// <summary>
    ///     The interface for ws client
    /// </summary>
    public interface IHassClient : IAsyncDisposable
    {
        /// <summary>
        ///     The current states of the entities.
        /// </summary>
        /// <remarks>Can be fully loaded when connecting by setting getStatesOnConnect=true</remarks>
        ConcurrentDictionary<string, HassState> States { get; }

        /// <summary>
        ///     Calls a service to home assistant
        /// </summary>
        /// <param name="domain">The domain for the servie, example "light"</param>
        /// <param name="service">The service to call, example "turn_on"</param>
        /// <param name="serviceData">The service data, use anonumous types, se example</param>
        /// <param name="waitForResponse">If true, it wait for the response from Hass else just ignore</param>
        /// <example>
        ///     Following example turn on light
        ///     <code>
        ///         var client = new HassClient();
        ///         await client.ConnectAsync("192.168.1.2", 8123, false);
        ///         await client.CallService("light", "turn_on", new {entity_id="light.myawesomelight"});
        ///         await client.CloseAsync();
        ///     </code>
        /// </example>
        /// <returns>True if successfully called service</returns>
        Task<bool> CallService(string domain, string service, object serviceData, bool waitForResponse = true);

        /// <summary>
        ///     Gracefully closes the connection to Home Assistant
        /// </summary>
        Task CloseAsync();

        /// <summary>
        ///     Connect to Home Assistant
        /// </summary>
        /// <param name="host">The host or ip address of Home Assistant</param>
        /// <param name="port">The port of Home Assistant, typically 8123 or 80</param>
        /// <param name="ssl">Set to true if Home Assistant using ssl (recommended secure setup for Home Assistant)</param>
        /// <param name="token">Authtoken from Home Assistant for access</param>
        /// <param name="getStatesOnConnect">Reads all states initially, this is the default behaviour</param>
        /// <returns>Returns true if successfully connected</returns>
        Task<bool> ConnectAsync(string host, short port, bool ssl, string token, bool getStatesOnConnect);

        /// <summary>
        ///     Connect to Home Assistant
        /// </summary>
        /// <param name="url">The uri of the websocket, typically ws://ip:8123/api/websocket</param>
        /// <param name="token">Authtoken from Home Assistant for access</param>
        /// <param name="getStatesOnConnect">Reads all states initially, this is the default behaviour</param>
        /// <returns>Returns true if successfully connected</returns>
        Task<bool> ConnectAsync(Uri url, string token, bool getStatesOnConnect);

        /// <summary>
        ///     Gets the configuration of the connected Home Assistant instance
        /// </summary>
        Task<HassConfig> GetConfig();

        /// <summary>
        ///     Gets the configuration of the connected Home Assistant instance
        /// </summary>
        Task<IEnumerable<HassServiceDomain>> GetServices();

        /// <summary>
        ///     Pings Home Assistant to check if connection is alive
        /// </summary>
        /// <param name="timeout">The timeout to wait for Home Assistant to return pong message</param>
        /// <returns>True if connection is alive.</returns>
        Task<bool> PingAsync(int timeout);

        /// <summary>
        ///     Returns next incoming event and completes async operation
        /// </summary>
        /// <remarks>Set subscribeEvents=true on ConnectAsync to use.</remarks>
        /// <exception>OperationCanceledException if the operation is canceled.</exception>
        /// <returns>Returns next event</returns>
        Task<HassEvent> ReadEventAsync();

        /// <summary>
        ///     Returns next incoming event and completes async operation
        /// </summary>
        /// <remarks>Set subscribeEvents=true on ConnectAsync to use.</remarks>
        /// <exception>OperationCanceledException if the operation is canceled.</exception>
        /// <param name="token">Cancellation token to provide cancellation</param>
        /// <returns>Returns next event</returns>
        Task<HassEvent> ReadEventAsync(CancellationToken token);

        /// <summary>
        ///     Sends custom event on Home Assistant eventbus
        /// </summary>
        /// <param name="eventId">Event identifier</param>
        /// <param name="data">Data being sent with the event</param>
        /// <returns></returns>
        Task<bool> SendEvent(string eventId, object? data = null);

        /// <summary>
        ///     Sets the state of an entity
        /// </summary>
        /// <param name="entityId">The id</param>
        /// <param name="attributes"></param>
        /// <returns>Returns the full state object from Home Assistant</returns>
        Task<HassState?> SetState(string entityId, string state, object? attributes);

        /// <summary>
        ///     Subscribe to all or single events from HomeAssistant
        /// </summary>
        /// <param name="eventType">The type of event subscribed to</param>
        /// <returns>Returns true if successful</returns>
        Task<bool> SubscribeToEvents(EventType eventType = EventType.All);
    }

    /// <summary>
    ///     Hides the internals of websocket connection
    ///     to connect, send and receive json messages
    ///     This class is threadsafe
    /// </summary>
    public class HassClient : IHassClient
    {
        /// <summary>
        ///     Used to cancel all asynchronous work, is internal so we can test
        /// </summary>
        internal CancellationTokenSource CancelSource = new CancellationTokenSource();

        /// <summary>
        ///     Default size for channel
        /// </summary>
        private const int DefaultChannelSize = 200;

        /// <summary>
        ///     Default read buffer size for websockets
        /// </summary>
        private const int DefaultReceiveBufferSize = 1024 * 4;

        /// <summary>
        ///     The default timeout for websockets
        /// </summary>
        private const int DefaultTimeout = 5000;

        /// <summary>
        ///     The max time we will wait for the socket to gracefully close
        /// </summary>
        private const int MaxWaitTimeSocketClose = 5000; // 5 seconds
                                                         // 5 seconds

        /// <summary>
        ///     Thread safe dicitionary that holds information about all command and command id:s
        ///     Is used to correclty deserialize the result messages from commands.
        /// </summary>
        private readonly ConcurrentDictionary<int, string> _commandsSent =
            new ConcurrentDictionary<int, string>(32, 200);

        /// <summary>
        ///     Thread safe dicitionary that holds information about all command and command id:s
        ///     Is used to correclty deserialize the result messages from commands.
        /// </summary>
        private readonly ConcurrentDictionary<int, string> _commandsSentAndResponseShouldBeDisregarded =
            new ConcurrentDictionary<int, string>(32, 200);


        /// <summary>
        ///     Default Json serialization options, Hass expects intended
        /// </summary>
        private readonly JsonSerializerOptions _defaultSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            IgnoreNullValues = true
        };

        /// <summary>
        ///     The http client used for post and get operations through the Home Assistant API
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        ///     The logger to use
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        ///     Set loglevel om messages when tracing is enabled
        ///     - Default, logs but state chages
        ///     - None, do not log messages
        ///     - All, logs all but security challanges
        /// </summary>
        private readonly string _messageLogLevel;

        /// <summary>
        ///     Channel used as a async thread safe way to wite messages to the websocket
        /// </summary>
        private readonly Channel<HassMessageBase> _writeChannel =
            Channel.CreateBounded<HassMessageBase>(DefaultChannelSize);

        private readonly IClientWebSocketFactory _wsFactory;

        /// <summary>
        ///     Base url to the API (non socket)
        /// </summary>
        private string _apiUrl = "";

        /// <summary>
        ///     Channel used as a async thread safe way to read messages from the websocket
        /// </summary>
        private Channel<HassEvent> _eventChannel = Channel.CreateBounded<HassEvent>(DefaultChannelSize);

        /// <summary>
        ///     Indicates if we are in the process of closing the socket and cleaning up resources
        ///     Avoids recursive states
        /// </summary>
        private bool _isClosing;

        /// <summary>
        ///     Channel used as a async thread safe way to read resultmessages from the websocket
        /// </summary>
        private Channel<HassMessage> _messageChannel = Channel.CreateBounded<HassMessage>(DefaultChannelSize);

        /// <summary>
        ///     Message id sent in command messages
        /// </summary>
        /// <remarks>Message id need to be increased every time it sends an command</remarks>
        private int _messageId = 1;

        /// <summary>
        ///     Async task to read all incoming messages
        /// </summary>
        private Task? _readMessagePumpTask;

        /// <summary>
        ///     Async task to write messages
        /// </summary>
        private Task? _writeMessagePumpTask;

        /// <summary>
        ///     The underlying currently connected socket or null if not connected
        /// </summary>
        private IClientWebSocket? _ws;
        /// <summary>
        ///     Instance a new HassClient
        /// </summary>
        /// <param name="logFactory">The LogFactory to use for logging, null uses default values from config.</param>
        public HassClient(ILoggerFactory? logFactory = null) :
            this(logFactory, new ClientWebSocketFactory(), null)
        { }

        /// <summary>
        ///     Instance a new HassClient
        /// </summary>
        /// <param name="logFactory">The LogFactory to use for logging, null uses default values from config.</param>
        /// <param name="wsFactory">The factory to use for websockets, mainly for testing purposes</param>
        /// <param name="httpMessageHandler">httpMessage handler (used for mocking)</param>
        internal HassClient(ILoggerFactory? logFactory, IClientWebSocketFactory? wsFactory, HttpMessageHandler? httpMessageHandler)
        {
            logFactory ??= _getDefaultLoggerFactory;
            wsFactory ??= new ClientWebSocketFactory();
            _httpClient = httpMessageHandler != null ?
                new HttpClient(httpMessageHandler) : new HttpClient();

            _wsFactory = wsFactory;
            _logger = logFactory.CreateLogger<HassClient>();
            _messageLogLevel = Environment.GetEnvironmentVariable("HASSCLIENT_MSGLOGLEVEL") ?? "Default";
        }

        /// <summary>
        ///     The current states of the entities.
        /// </summary>
        public ConcurrentDictionary<string, HassState> States { get; } =
            new ConcurrentDictionary<string, HassState>(Environment.ProcessorCount * 2, DefaultChannelSize);

        /// <summary>
        ///     Internal property for tests to access the timeout during unit testing
        /// </summary>
        internal int SocketTimeout { get; set; } = DefaultTimeout;

        /// <summary>
        ///     The default logger
        /// </summary>
        private static ILoggerFactory _getDefaultLoggerFactory => LoggerFactory.Create(builder =>
        {
            builder
                .ClearProviders()
                .AddFilter("HassClient.HassClient", LogLevel.Information)
                .AddConsole();
        });

        /// <summary>
        ///     Calls a service to home assistant
        /// </summary>
        /// <param name="domain">The domain for the servie, example "light"</param>
        /// <param name="service">The service to call, example "turn_on"</param>
        /// <param name="serviceData">The service data, use anonymous types, se example</param>
        /// <param name="waitForResponse">If true, it wait for the response from Hass else just ignore</param>
        /// <example>
        ///     Following example turn on light
        ///     <code>
        /// var client = new HassClient();
        /// await client.ConnectAsync("192.168.1.2", 8123, false);
        /// await client.CallService("light", "turn_on", new {entity_id="light.myawesomelight"});
        /// await client.CloseAsync();
        /// </code>
        /// </example>
        /// <returns>True if successfully called service</returns>
        public async Task<bool> CallService(string domain, string service, object serviceData, bool waitForResponse = true)
        {
            try
            {
                HassMessage result = await SendCommandAndWaitForResponse(new CallServiceCommand
                {
                    Domain = domain,
                    Service = service,
                    ServiceData = serviceData
                }, waitForResponse);
                return result.Success ?? false;
            }
            catch (OperationCanceledException)
            {
                if (CancelSource.IsCancellationRequested)
                {
                    throw;
                }

                return false; // Just timeout not canceled
            }
        }

        /// <summary>
        ///     Closes the websocket
        /// </summary>
        public async Task CloseAsync()
        {
            lock (this)
            {
                if (_isClosing || _ws == null)
                {
                    // Already closed
                    return;
                }

                _isClosing = true;
            }

            try
            {
                _logger.LogTrace("Async close websocket");

                // First do websocket close management
                var timeout = new CancellationTokenSource(MaxWaitTimeSocketClose);

                try
                {
                    if (
                        _ws.State == WebSocketState.Open ||
                        _ws.State == WebSocketState.CloseReceived ||
                        _ws.State == WebSocketState.CloseSent
                        )
                    {
                        // after this, the socket state which change to CloseSent
                        await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
                        // now we wait for the server response, which will close the socket
                        while (_ws.State != WebSocketState.Closed && !timeout.Token.IsCancellationRequested)
                            await Task.Delay(100).ConfigureAwait(false);
                    }

                }
                catch (OperationCanceledException)
                {
                    // normal upon task/token cancellation, disregard
                }

                // Cancel all async stuff
                CancelSource.Cancel();

                // Wait for read and write tasks to complete max 5 seconds
                if (_readMessagePumpTask != null && _writeMessagePumpTask != null)
                {
                    await Task.WhenAll(_readMessagePumpTask, _writeMessagePumpTask);
                }
            }
            catch
            {

                throw;
            }
            finally
            {
                if (_ws != null)
                    _ws.Dispose();
                _ws = null;

                if (CancelSource != null)
                    CancelSource.Dispose();

                CancelSource = new CancellationTokenSource();

                _logger.LogTrace("Async close websocket done");
                _isClosing = false;
            }
        }

        /// <summary>
        ///     Connect to Home Assistant
        /// </summary>
        /// <param name="host">The host or ip address of Home Assistant</param>
        /// <param name="port">The port of Home Assistant, typically 8123 or 80</param>
        /// <param name="ssl">Set to true if Home Assistant using ssl (recommended secure setup for Home Assistant)</param>
        /// <param name="token">Authtoken from Home Assistant for access</param>
        /// <param name="getStatesOnConnect">Reads all states initially, this is the default behaviour</param>
        /// <returns>Returns true if successfully connected</returns>
        public Task<bool> ConnectAsync(string host, short port, bool ssl, string token, bool getStatesOnConnect) =>
            ConnectAsync(new Uri($"{(ssl ? "wss" : "ws")}://{host}:{port}/api/websocket"), token, getStatesOnConnect);

        /// <summary>
        ///     Connect to Home Assistant
        /// </summary>
        /// <param name="url">The uri of the websocket</param>
        /// <param name="token">Authtoken from Home Assistant for access</param>
        /// <param name="getStatesOnConnect">Reads all states initially, this is the default behaviour</param>
        /// <returns>Returns true if successfully connected</returns>
        public async Task<bool> ConnectAsync(Uri url, string token,
            bool getStatesOnConnect = true)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url), "Expected url to be provided");
            }

            var httpScheme = (url.Scheme == "ws") ? "http" : "https";

            if (url.Host == "supervisor")
            {
                // Todo: DO NOT HARD CODE URLs NOOB!
                _apiUrl = "http://supervisor/core/api";
            }
            else
            {
                _apiUrl = $"{httpScheme}://{url.Host}:{url.Port}/api";
            }


            // Check if we already have a websocket running
            if (_ws != null)
            {
                throw new InvalidOperationException("Already connected to the remote websocket.");
            }

            // Setup default headers for httpclient
            if (_httpClient != null)
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            }

            try
            {
                IClientWebSocket ws = _wsFactory.New() ?? throw new NullReferenceException("Websocket cant be null!");
                using var timerTokenSource = new CancellationTokenSource(SocketTimeout);
                // Make a combined token source with timer and the general cancel token source
                // The operations will cancel from ether one
                using var connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    timerTokenSource.Token, CancelSource.Token);

                await ws.ConnectAsync(url, connectTokenSource.Token);

                if (ws.State == WebSocketState.Open)
                {
                    // Initialize the correct states when successfully connecting to the websocket
                    InitStatesOnConnect(ws);

                    // Do the authenticate and get the auhtorization response
                    HassMessage result = await HandleConnectAndAuthenticate(token, connectTokenSource);

                    switch (result.Type)
                    {
                        case "auth_ok":
                            if (getStatesOnConnect)
                            {
                                await GetStates(connectTokenSource);
                            }

                            _logger.LogTrace($"Connected to websocket ({url}) on host {url.Host} and the api ({_apiUrl})");
                            return true;

                        case "auth_invalid":
                            _logger.LogError($"Failed to authenticate ({result.Message})");
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

        /// <summary>
        ///     Gets the configuration of the connected Home Assistant instance
        /// </summary>
        public async Task<HassConfig> GetConfig()
        {
            HassMessage hassResult = await SendCommandAndWaitForResponse(new GetConfigCommand());

            object resultMessage =
                hassResult.Result ?? throw new ApplicationException("Unexpected response from command");

            var result = resultMessage as HassConfig;
            if (result != null)
            {
                return result;
            }

            throw new ApplicationException($"The result not expected! {resultMessage}");
        }

        /// <summary>
        ///     Pings Home Assistant to check if connection is alive
        /// </summary>
        /// <param name="timeout">The timeout to wait for Home Assistant to return pong message</param>
        /// <returns>True if connection is alive.</returns>
        public async Task<bool> PingAsync(int timeout)
        {
            using var timerTokenSource = new CancellationTokenSource(timeout);
            // Make a combined token source with timer and the general cancel token source
            // The operations will cancel from ether one
            using var pingTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                timerTokenSource.Token, CancelSource.Token);

            try
            {
                if (SendMessage(new HassPingCommand()))
                {
                    HassMessage result = await _messageChannel.Reader.ReadAsync(pingTokenSource.Token);
                    if (result.Type == "pong")
                    {
                        return true;
                    }
                }
            }
            catch (OperationCanceledException) { } // Do nothing

            return false;
        }

        /// <summary>
        ///     Returns next incoming event and completes async operation
        /// </summary>
        /// <remarks>Set subscribeEvents=true on ConnectAsync to use.</remarks>
        /// <exception>OperationCanceledException if the operation is canceled.</exception>
        /// <returns>Returns next event</returns>
        public async Task<HassEvent> ReadEventAsync() => await _eventChannel.Reader.ReadAsync(CancelSource.Token);

        /// <summary>
        ///     Returns next incoming event and completes async operation
        /// </summary>
        /// <remarks>Set subscribeEvents=true on ConnectAsync to use.</remarks>
        /// <exception>OperationCanceledException if the operation is canceled.</exception>
        /// <returns>Returns next event</returns>
        public async Task<HassEvent> ReadEventAsync(CancellationToken token)
        {
            using var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(CancelSource.Token, token);
            return await _eventChannel.Reader.ReadAsync(cancelSource.Token);
        }

        public async Task<bool> SendEvent(string eventId, object? data = null)
        {
            var apiUrl = $"{_apiUrl}/events/{HttpUtility.UrlEncode(eventId)}";
            var content = "";

            if (data != null)
            {
                content = JsonSerializer.Serialize<object>(data, _defaultSerializerOptions);
            }
            try
            {
                var result = await _httpClient.PostAsync(new Uri(apiUrl),
                    new StringContent(content, Encoding.UTF8),
                    CancelSource.Token);

                if (result.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to set state");
            }
            return false;
        }


        public async Task<HassState?> SetState(string entityId, string state, object? attributes)
        {

            var apiUrl = $"{_apiUrl}/states/{HttpUtility.UrlEncode(entityId)}";
            var content = JsonSerializer.Serialize<object>(
                    new
                    {
                        state = state,
                        attributes = attributes
                    }, _defaultSerializerOptions);

            try
            {
                var result = await _httpClient.PostAsync(new Uri(apiUrl),
                    new StringContent(content, Encoding.UTF8),
                    CancelSource.Token);

                if (result.IsSuccessStatusCode)
                {
                    var hassState = await JsonSerializer.DeserializeAsync<HassState>(await result.Content.ReadAsStreamAsync(),
                        _defaultSerializerOptions);

                    return hassState;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to set state");
            }
            return null;
        }

        public async Task<bool> SubscribeToEvents(EventType eventType = EventType.All)
        {
            var command = new SubscribeEventCommand();

            if (eventType != EventType.All)
            {
                switch (eventType)
                {
                    case EventType.HomeAssistantStart:
                        command.EventType = "homeassistant_start";
                        break;
                    case EventType.HomeAssistantStop:
                        command.EventType = "homeassistant_stop";
                        break;
                    case EventType.StateChanged:
                        command.EventType = "state_changed";
                        break;
                    case EventType.ServiceRegistered:
                        command.EventType = "service_registered";
                        break;
                    case EventType.CallService:
                        command.EventType = "call_service";
                        break;
                    case EventType.ServiceExecuted:
                        command.EventType = "service_executed";
                        break;
                    case EventType.PlatformDiscovered:
                        command.EventType = "platform_discovered";
                        break;
                    case EventType.ComponentLoaded:
                        command.EventType = "component_loaded";
                        break;
                }
            }

            var result = await SendCommandAndWaitForResponse(command);
            return result.Success ?? false;
        }

        public async Task<IEnumerable<HassServiceDomain>> GetServices()
        {

            HassMessage servicesResult = await SendCommandAndWaitForResponse(new GetServicesCommand());

            var resultMessage =
                servicesResult.Result as IEnumerable<HassServiceDomain>
                    ?? throw new ApplicationException("Unexpected response from GetServices command");

            return resultMessage;
        }

        /// <summary>
        ///     Process next message from Home Assistant
        /// </summary>
        /// <remarks>
        ///     Uses Pipes to allocate memory where the websocket writes to and
        ///     Write the read message to a channel.
        /// </remarks>
        /// <returns></returns>
        internal virtual async Task ProcessNextMessage()
        {
            var pipe = new Pipe();

            using var cancelProcessNextMessage = new CancellationTokenSource();

            using var cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancelProcessNextMessage.Token, CancelSource.Token);

            await Task.WhenAll(
                Task.Run(ReadFromClientSocket, cancelTokenSource.Token),
                Task.Run(WriteMessagesToChannel, cancelTokenSource.Token)
            );

            // Task that reads the next message from websocket
            async Task ReadFromClientSocket()
            {
                try
                {
                    while (_ws != null && (!CancelSource.Token.IsCancellationRequested && !_ws.CloseStatus.HasValue))
                    {
                        Memory<byte> memory = pipe.Writer.GetMemory(DefaultReceiveBufferSize);

                        // ReSharper disable once AccessToDisposedClosure
                        ValueWebSocketReceiveResult result = await _ws.ReceiveAsync(memory, cancelTokenSource.Token);
                        if (!CancelSource.Token.IsCancellationRequested)
                        {
                            if (_ws.State == WebSocketState.CloseReceived && result.MessageType == WebSocketMessageType.Close)
                            {
                                await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
                                CancelSource.Cancel();
                            }
                            else
                            {
                                if (_ws.State == WebSocketState.Open && result.MessageType != WebSocketMessageType.Close)
                                {
                                    // Log incoming messages for correct loglevel and tracing is enabled
                                    if (_messageLogLevel != "None" && _logger.IsEnabled(LogLevel.Trace) && result.Count > 0)
                                    {
                                        var strMessageReceived = UTF8Encoding.UTF8.GetString(memory.Slice(0, result.Count).ToArray());
                                        if (_messageLogLevel == "All")
                                            _logger.LogTrace("ReadClientSocket, message: {strMessageReceived}", strMessageReceived);
                                        else if (_messageLogLevel == "Default")
                                        {
                                            // Log all but events
                                            if (strMessageReceived.Contains("\"type\": \"event\"") == false)
                                                _logger.LogTrace("ReadClientSocket, message: {strMessageReceived}", strMessageReceived);
                                        }
                                    }
                                    // Advance writer to the read ne of bytes
                                    pipe.Writer.Advance(result.Count);

                                    await pipe.Writer.FlushAsync();

                                    if (result.EndOfMessage)
                                    {
                                        // We have successfully read the whole message, make available to reader
                                        await pipe.Writer.CompleteAsync();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Canceled the thread just leave
                    throw;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Major failure in ReadFromClientSocket, exit...");
                    // Make sure we always cancel the other task of any reason
                    // ReSharper disable once AccessToDisposedClosure
                    cancelProcessNextMessage.Cancel(true);
                    throw;
                }

            }

            // Task that deserializes the message and write the finished message to a channel
            async Task WriteMessagesToChannel()
            {
                try
                {
                    // ReSharper disable once AccessToDisposedClosure
                    HassMessage m = await JsonSerializer.DeserializeAsync<HassMessage>(pipe.Reader.AsStream(),
                        cancellationToken: cancelTokenSource.Token);

                    await pipe.Reader.CompleteAsync();
                    switch (m.Type)
                    {
                        case "event":
                            if (m.Event != null)
                            {
                                _eventChannel.Writer.TryWrite(m.Event);
                            }

                            break;
                        case "auth_required":
                        case "auth_ok":
                        case "auth_invalid":
                        case "call_service":
                        case "get_config":
                        case "pong":
                            _messageChannel.Writer.TryWrite(m);
                            break;
                        case "result":
                            var resultMessage = GetResultMessage(m);
                            if (resultMessage != null)
                                _messageChannel.Writer.TryWrite(resultMessage);
                            break;
                        default:
                            _logger.LogDebug($"Unexpected eventtype {m.Type}, discarding message!");
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Canceled the thread just leave
                }
                catch (Exception e)
                {
                    // Todo: Log the seralizer error here later but continue receive
                    // messages from the server. Then we can survive the server
                    // Sending bad json messages
                    _logger.LogDebug(e, "Error deserialize json response");
                    // Make sure we put a small delay incase we have severe error so the loop
                    // doesn't kill the server

                    // ReSharper disable once AccessToDisposedClosure
                    await Task.Delay(20, cancelTokenSource.Token);
                    // ReSharper disable once AccessToDisposedClosure
                    cancelProcessNextMessage.Cancel();
                }
            }
        }

        internal virtual async ValueTask<HassMessage> SendCommandAndWaitForResponse(CommandMessage message, bool waitForResponse = true)
        {
            using var timerTokenSource = new CancellationTokenSource(SocketTimeout);
            // Make a combined token source with timer and the general cancel token source
            // The operations will cancel from ether one
            using var sendCommandTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                timerTokenSource.Token, CancelSource.Token);

            try
            {
                if (!SendMessage(message, waitForResponse))
                    throw new ApplicationException($"Send message {message.Type} failed!");

                if (!waitForResponse)
                    return new HassMessage { Success = true };

                while (true)
                {
                    HassMessage result = await _messageChannel.Reader.ReadAsync(sendCommandTokenSource.Token);
                    if (result.Id == message.Id)
                    {
                        return result;
                    }

                    // Not the response, push message back
                    bool res = _messageChannel.Writer.TryWrite(result);

                    if (!res)
                    {
                        throw new ApplicationException("Failed to write to message channel!");
                    }


                    // Delay for a short period to let the message arrive we are searching for
                    await Task.Delay(10);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogError($"Fail to send command {message.Type} and receive correct command within timeout. ");
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError($"Fail to send command {message.Type}. ");
                _logger.LogDebug(e, "Fail to send command.");
                throw;
            }
        }

        /// <summary>
        ///     Send message and correctly handle message id counter
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="waitForResponse">True if sender expects response</param>
        /// <returns>True if successful</returns>
        internal virtual bool SendMessage(HassMessageBase message, bool waitForResponse = true)
        {
            _logger.LogTrace($"Sends message {message.Type}");
            if (message is CommandMessage commandMessage)
            {
                commandMessage.Id = ++_messageId;
                if (waitForResponse)
                {
                    //We save the type of command so we can deserialize the correct message later
                    _commandsSent[_messageId] = commandMessage.Type;
                }
                else
                {
                    _commandsSentAndResponseShouldBeDisregarded[_messageId] = commandMessage.Type; ;
                }
            }

            return _writeChannel.Writer.TryWrite(message);
        }

        /// <summary>
        ///     Get the correct result message from HassMessage
        /// </summary>
        private HassMessage? GetResultMessage(HassMessage m)
        {
            if (m.Id > 0)
            {

                // It is an command response, get command
                if (_commandsSent.TryRemove(m.Id, out string? command))
                {
                    if (m.Success == false)
                    {
                        _logger.LogWarning($"Non successful result for command {command}: code({m.Error?.Code}), message: {m.Error?.Message}");
                    }
                    switch (command)
                    {
                        case "get_states":
                            m.Result = m.ResultElement?.ToHassStates(_defaultSerializerOptions);
                            break;
                        case "get_config":
                            m.Result = m.ResultElement?.ToObject<HassConfig>(_defaultSerializerOptions);
                            break;
                        case "get_services":
                            m.Result = m.ResultElement?.ToServicesResult();
                            break;
                        case "subscribe_events":
                            break; // Do nothing
                        case "call_service":
                            break; // Do nothing
                        default:
                            _logger.LogError($"The command message {command} is not supported");
                            break;
                    }
                    m.ResultElement = null;
                }
                else
                {
                    string? originalCommand;
                    var resultMsg = _commandsSentAndResponseShouldBeDisregarded.TryRemove(m.Id, out originalCommand) ? null : m;

                    if (m.Success == false)
                    {
                        _logger.LogWarning($"Non successful unwaited result for command {originalCommand}: code({m.Error?.Code}), message: {m.Error?.Message}");
                    }
                    // Make sure we discard messages that no one is waiting for
                    return resultMsg;
                }
            }

            return m;
        }

        private async Task GetStates(CancellationTokenSource connectTokenSource)
        {
            SendMessage(new GetStatesCommand());
            HassMessage result = await _messageChannel.Reader.ReadAsync(connectTokenSource.Token);
            if (result?.Result is List<HassState> wsResult)
            {
                foreach (HassState state in wsResult)
                {
                    States[state.EntityId] = state;
                }
            }
        }

        private async Task<HassMessage> HandleConnectAndAuthenticate(string token,
            CancellationTokenSource connectTokenSource)
        {
            HassMessage result = await _messageChannel.Reader.ReadAsync(connectTokenSource.Token);
            if (result.Type == "auth_required")
            {
                SendMessage(new HassAuthMessage { AccessToken = token });
                result = await _messageChannel.Reader.ReadAsync(connectTokenSource.Token);
            }

            return result;
        }

        private void InitStatesOnConnect(IClientWebSocket ws)
        {
            _ws = ws;
            _messageId = 1;

            _isClosing = false;

            // Make sure we have new channels so we are not have old messages
            _messageChannel = Channel.CreateBounded<HassMessage>(DefaultChannelSize);
            _eventChannel = Channel.CreateBounded<HassEvent>(DefaultChannelSize);

            CancelSource = new CancellationTokenSource();
            _readMessagePumpTask = Task.Run(ReadMessagePump);
            _writeMessagePumpTask = Task.Run(WriteMessagePump);
        }
        /// <summary>
        ///     A pump that reads incoming messages and put them on the read channel.
        /// </summary>
        private async Task ReadMessagePump()
        {
            _logger.LogTrace("Start ReadMessagePump");

            // While not canceled and websocket is not closed
            while (_ws != null && (!CancelSource.IsCancellationRequested && !_ws.CloseStatus.HasValue))
            {
                try
                {
                    await ProcessNextMessage();
                }
                catch (OperationCanceledException)
                {
                    // Canceled the thread just leave
                    break;
                }

                // Should never cast any other exception, if so it just not handle them here
            }

            _logger.LogTrace("Exit ReadMessagePump");
        }
        private async Task WriteMessagePump()
        {
            _logger.LogTrace("Start WriteMessagePump");

            while (_ws != null && (!CancelSource.IsCancellationRequested && !_ws.CloseStatus.HasValue))
            {
                try
                {
                    HassMessageBase nextMessage = await _writeChannel.Reader.ReadAsync(CancelSource.Token);

                    if (_ws.State != WebSocketState.Open && _ws.State != WebSocketState.CloseReceived)
                    {
                        _logger.LogTrace("WriteMessagePump, state not Open or CloseReceived, exiting WriteMessagePump: {socketState}", _ws.State.ToString());
                        return;
                    }

                    byte[] result = JsonSerializer.SerializeToUtf8Bytes(nextMessage, nextMessage.GetType(),
                        _defaultSerializerOptions);

                    await _ws.SendAsync(result, WebSocketMessageType.Text, true, CancelSource.Token);

                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        if (nextMessage is HassAuthMessage == false)
                        {
                            // We log everything but AuthMessage due to security reasons
                            _logger.LogTrace("SendAsync, message: {result}", UTF8Encoding.UTF8.GetString(result));
                        }
                        {
                            _logger.LogTrace("Sending auth message, not shown for security reasons");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Canceled the thread
                    break;
                }
            }

            _logger.LogTrace("Exit WriteMessagePump");
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await CloseAsync();
            }
            catch
            {
                // Ignore errors
            }


        }
    }
}