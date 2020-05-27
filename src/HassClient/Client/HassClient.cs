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
        /// <remarks>
        /// Can be fully loaded when connecting by setting getStatesOnConnect=true,
        /// beware that it is not maintained after that initial load
        /// </remarks>
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
        ///     Gets all registered Areas from Home Assistant
        /// </summary>
        Task<HassAreas> GetAreas();

        /// <summary>
        ///     Gets all registered Devices from Home Assistant
        /// </summary>
        Task<HassDevices> GetDevices();

        /// <summary>
        ///     Gets all registered Entities from entity registry from Home Assistant
        /// </summary>
        Task<HassEntities> GetEntities();

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

        /// <summary>
        ///     Get all state for all entities in Home Assistant
        /// </summary>
        /// <param name="token">Provided token</param>
        Task<IEnumerable<HassState>> GetAllStates(CancellationToken? token = null);
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

        private readonly IClientWebSocketFactory _wsFactory;
        private readonly ITransportPipelineFactory<HassMessage>? _pipelineFactory;
        private readonly ILoggerFactory _loggerFactory;

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
        private ITransportPipeline<HassMessage>? _messagePipeline;

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
        ///     The underlying currently connected socket or null if not connected
        /// </summary>
        private IClientWebSocket? _ws = null;

        /// <summary>
        ///     Instance a new HassClient
        /// </summary>
        /// <param name="logFactory">The LogFactory to use for logging, null uses default values from config.</param>
        public HassClient(ILoggerFactory? logFactory = null) :
            this(logFactory, new WebSocketMessagePipelineFactory<HassMessage>(), new ClientWebSocketFactory(), null)
        { }

        /// <summary>
        ///     Instance a new HassClient
        /// </summary>
        /// <param name="logFactory">The LogFactory to use for logging, null uses default values from config.</param>
        /// <param name="wsFactory">The factory to use for websockets, mainly for testing purposes</param>
        /// <param name="httpMessageHandler">httpMessage handler (used for mocking)</param>
        internal HassClient(
            ILoggerFactory? logFactory,
            ITransportPipelineFactory<HassMessage>? pipelineFactory,
            IClientWebSocketFactory? wsFactory,
            HttpMessageHandler? httpMessageHandler)
        {
            logFactory ??= _getDefaultLoggerFactory;
            wsFactory ??= new ClientWebSocketFactory();
            _httpClient = httpMessageHandler != null ?
                new HttpClient(httpMessageHandler) : new HttpClient();

            _wsFactory = wsFactory;
            _pipelineFactory = pipelineFactory;

            _loggerFactory = logFactory;
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
                }, waitForResponse).ConfigureAwait(false);
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
                        await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token).ConfigureAwait(false);
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

                if (_messagePipeline is object)
                    await _messagePipeline.CloseAsync().ConfigureAwait(false);

                // Wait for read and write tasks to complete max 5 seconds
                if (_readMessagePumpTask is object)
                {
                    await _readMessagePumpTask.ConfigureAwait(false);
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

                if (_messagePipeline is object)
                    await _messagePipeline.DisposeAsync().ConfigureAwait(false);

                _messagePipeline = null;

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

                await ws.ConnectAsync(url, connectTokenSource.Token).ConfigureAwait(false);

                if (ws.State == WebSocketState.Open)
                {
                    // Initialize the correct states when successfully connecting to the websocket
                    InitStatesOnConnect(ws);

                    // Do the authenticate and get the auhtorization response
                    HassMessage result = await HandleConnectAndAuthenticate(token, connectTokenSource).ConfigureAwait(false);

                    switch (result.Type)
                    {
                        case "auth_ok":
                            if (getStatesOnConnect)
                            {
                                var currentStates = await GetAllStates(connectTokenSource.Token).ConfigureAwait(false);
                                foreach (var state in currentStates)
                                {
                                    States[state.EntityId] = state;
                                }
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
            HassMessage hassResult = await SendCommandAndWaitForResponse(new GetConfigCommand()).ConfigureAwait(false);

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
                await SendMessage(new HassPingCommand()).ConfigureAwait(false);
                HassMessage result = await _messageChannel.Reader.ReadAsync(pingTokenSource.Token).ConfigureAwait(false);
                if (result.Type == "pong")
                {
                    return true;
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
        public async Task<HassEvent> ReadEventAsync() => await _eventChannel.Reader.ReadAsync(CancelSource.Token).ConfigureAwait(false);

        /// <summary>
        ///     Returns next incoming event and completes async operation
        /// </summary>
        /// <remarks>Set subscribeEvents=true on ConnectAsync to use.</remarks>
        /// <exception>OperationCanceledException if the operation is canceled.</exception>
        /// <returns>Returns next event</returns>
        public async Task<HassEvent> ReadEventAsync(CancellationToken token)
        {
            using var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(CancelSource.Token, token);
            return await _eventChannel.Reader.ReadAsync(cancelSource.Token).ConfigureAwait(false);
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
                    CancelSource.Token).ConfigureAwait(false);

                if (result.IsSuccessStatusCode)
                {
                    var hassState = await JsonSerializer.DeserializeAsync<HassState>(await result.Content.ReadAsStreamAsync().ConfigureAwait(false),
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

            var result = await SendCommandAndWaitForResponse(command).ConfigureAwait(false);
            return result.Success ?? false;
        }

        public async Task<IEnumerable<HassServiceDomain>> GetServices()
        {

            HassMessage servicesResult = await SendCommandAndWaitForResponse(new GetServicesCommand()).ConfigureAwait(false);

            var resultMessage =
                servicesResult.Result as IEnumerable<HassServiceDomain>
                    ?? throw new ApplicationException("Unexpected response from GetServices command");

            return resultMessage;
        }

        public async Task<HassAreas> GetAreas()
        {

            HassMessage servicesResult = await SendCommandAndWaitForResponse(new GetAreasCommand()).ConfigureAwait(false);

            var resultMessage =
                servicesResult.Result as HassAreas
                    ?? throw new ApplicationException("Unexpected response from GetServices command");

            return resultMessage;
        }

        public async Task<HassDevices> GetDevices()
        {

            HassMessage devicesResult = await SendCommandAndWaitForResponse(new GetDevicesCommand()).ConfigureAwait(false);

            var resultMessage =
                devicesResult.Result as HassDevices
                    ?? throw new ApplicationException("Unexpected response from GetDevices command");

            return resultMessage;
        }

        public async Task<HassEntities> GetEntities()
        {

            HassMessage servicesResult = await SendCommandAndWaitForResponse(new GetEntitiesCommand()).ConfigureAwait(false);

            var resultMessage =
                servicesResult.Result as HassEntities
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
            if (_messagePipeline is null)
            {
                _logger.LogWarning("Processing message with no {pipeline} set! returning.", nameof(_messagePipeline));
                return;
            }


            try
            {
                // ReSharper disable once AccessToDisposedClosure
                var m = await _messagePipeline.GetNextMessageAsync(CancelSource.Token).ConfigureAwait(false);

                switch (m?.Type)
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
                        _logger.LogDebug($"Unexpected eventtype {m?.Type}, discarding message!");
                        break;
                }

            }
            catch (OperationCanceledException)
            {
                // Canceled the thread just leave
            }
            catch (Exception e)
            {
                // Sending bad json messages
                _logger.LogDebug(e, "Error deserialize json response");
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
                await SendMessage(message, waitForResponse).ConfigureAwait(false);

                if (!waitForResponse)
                    return new HassMessage { Success = true };

                while (true)
                {
                    HassMessage result = await _messageChannel.Reader.ReadAsync(sendCommandTokenSource.Token).ConfigureAwait(false);
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
                    await Task.Delay(10).ConfigureAwait(false);
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
        internal virtual Task SendMessage(HassMessageBase message, bool waitForResponse = true)
        {
            if (_messagePipeline is null)
            {
                _logger.LogWarning("SendMessage called with no {pipeline} set!", nameof(_messagePipeline));
                throw new ApplicationException($"SendMessage called with no {nameof(_messagePipeline)} set!");
            }

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
                    _commandsSentAndResponseShouldBeDisregarded[_messageId] = FormatCommand(commandMessage);
                }
            }

            return _messagePipeline.SendMessageAsync(message, CancelSource.Token);
        }

        private string FormatCommand(CommandMessage message)
        {
            return message switch
            {
                CallServiceCommand cc => $"call_service: {cc.Domain}.{cc.Service} ({cc.ServiceData})",
                _ => message.Type
            };
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
                        case "config/area_registry/list":
                            m.Result = m.ResultElement?.ToObject<HassAreas>(_defaultSerializerOptions);
                            break;
                        case "config/device_registry/list":
                            m.Result = m.ResultElement?.ToObject<HassDevices>(_defaultSerializerOptions);
                            break;
                        case "config/entity_registry/list":
                            m.Result = m.ResultElement?.ToObject<HassEntities>(_defaultSerializerOptions);
                            break;
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
        public async Task<IEnumerable<HassState>> GetAllStates(CancellationToken? token = null)
        {
            var tokenToUse = token ?? CancelSource.Token;

            await SendMessage(new GetStatesCommand()).ConfigureAwait(false);
            HassMessage result = await _messageChannel.Reader.ReadAsync(tokenToUse).ConfigureAwait(false);
            if (result.Result is object && result.Result is List<HassState> wsResult)
            {
                return (List<HassState>)result.Result;
            }

            return new List<HassState>();
        }

        private async Task<HassMessage> HandleConnectAndAuthenticate(string token,
            CancellationTokenSource connectTokenSource)
        {
            HassMessage result = await _messageChannel.Reader.ReadAsync(connectTokenSource.Token).ConfigureAwait(false);
            if (result.Type == "auth_required")
            {
                await SendMessage(new HassAuthMessage { AccessToken = token }).ConfigureAwait(false);
                result = await _messageChannel.Reader.ReadAsync(connectTokenSource.Token).ConfigureAwait(false);
            }

            return result;
        }

        private void InitStatesOnConnect(IClientWebSocket ws)
        {
            _ws = ws;

            _messageId = 1;

            _isClosing = false;

            _messagePipeline = _pipelineFactory?.CreateWebSocketMessagePipeline(_ws, _loggerFactory);
            // Make sure we have new channels so we are not have old messages
            _messageChannel = Channel.CreateBounded<HassMessage>(DefaultChannelSize);
            _eventChannel = Channel.CreateBounded<HassEvent>(DefaultChannelSize);

            CancelSource = new CancellationTokenSource();
            _readMessagePumpTask = Task.Run(ReadMessagePump);
            // _writeMessagePumpTask = Task.Run(WriteMessagePump);
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
                    await ProcessNextMessage().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Canceled the thread just leave
                    break;
                }

                // Should never cast any other exception, if so it just not handle them here
            }
            // Cancel rest of operation
            CancelSource.Cancel();

            _logger.LogTrace("Exit ReadMessagePump");
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}