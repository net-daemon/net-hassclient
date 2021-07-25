using JoySoftware.HomeAssistant.Helpers;
using JoySoftware.HomeAssistant.Helpers.Json;
using JoySoftware.HomeAssistant.Messages;
using JoySoftware.HomeAssistant.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        /// <param name="domain">The domain for the service, example "light"</param>
        /// <param name="service">The service to call, example "turn_on"</param>
        /// <param name="serviceData">The service data, use anonymous types, se example</param>
        /// <param name="target">The target entity, device or area</param>
        /// <param name="waitForResponse">If true, it wait for the response from Hass else just ignore</param>
        /// <example>
        ///     Following example turn on light
        ///     <code>
        ///         var client = new HassClient();
        ///         await client.ConnectAsync("192.168.1.2", 8123, false);
        ///         await client.CallService("light", "turn_on", target = new {entity_id=["light.myawesomelight"]});
        ///         await client.CloseAsync();
        ///     </code>
        /// </example>
        /// <returns>True if successfully called service</returns>
        Task<bool> CallService(string domain, string service, object? serviceData = null, HassTarget? target = null, bool waitForResponse = true);

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
        /// <param name="token">AuthToken from Home Assistant for access</param>
        /// <param name="getStatesOnConnect">Reads all states initially, this is the default behaviour</param>
        /// <returns>Returns true if successfully connected</returns>
        Task<bool> ConnectAsync(string host, short port, bool ssl, string token, bool getStatesOnConnect);

        /// <summary>
        ///     Connect to Home Assistant
        /// </summary>
        /// <param name="url">The uri of the websocket, typically ws://ip:8123/api/websocket</param>
        /// <param name="token">AuthToken from Home Assistant for access</param>
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
        Task<IReadOnlyCollection<HassArea>> GetAreas();

        /// <summary>
        ///     Gets all registered Devices from Home Assistant
        /// </summary>
        Task<IReadOnlyCollection<HassDevice>> GetDevices();

        /// <summary>
        ///     Get to Home Assistant API
        /// </summary>
        /// <param name="apiPath">relative path</param>
        /// <typeparam name="T">Return type (json serializable)</typeparam>
        Task<T?> GetApiCall<T>(string apiPath);

        /// <summary>
        ///     Post to Home Assistant API
        /// </summary>
        /// <param name="apiPath">relative path</param>
        /// <param name="data">data being sent</param>
        /// <typeparam name="T">Return type (json serializable)</typeparam>
        public Task<T?> PostApiCall<T>(string apiPath, object? data = null);

        /// <summary>
        ///     Trigger a state change using trigger templates
        /// </summary>
        /// <param name="id">webhook id</param>
        /// <param name="data">data being sent</param>
        public Task TriggerWebhook(string id, object? data);

        /// <summary>
        ///     Gets all registered Entities from entity registry from Home Assistant
        /// </summary>
        Task<IReadOnlyCollection<HassEntity>> GetEntities();

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
        /// <param name="state"></param>
        /// <param name="attributes"></param>
        /// <returns>Returns the full state object from Home Assistant</returns>
        Task<HassState?> SetState(string entityId, string state, object? attributes);

        /// <summary>
        ///     Sets the state of an entity
        /// </summary>
        /// <param name="entityId">The id</param>
        /// <returns>Returns the full state object from Home Assistant</returns>
        Task<HassState?> GetState(string entityId);

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
    ///     This class is thread safe
    /// </summary>
    public class HassClient : IHassClient
    {
        /// <summary>
        ///     Used to cancel all asynchronous work, is internal so we can test
        /// </summary>
        internal CancellationTokenSource CancelSource = new();

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

        /// <summary>
        ///     Used to make sure the client is not closed more than once
        /// </summary>
        private bool _isClosed;

        /// <summary>
        ///     Thread safe dictionary that holds information about all command and command id:s
        ///     Is used to correctly deserialize the result messages from commands.
        /// </summary>
        private readonly ConcurrentDictionary<int, CommandMessage> _commandsSent = new(32, 200);

        /// <summary>
        ///     Thread safe dictionary that holds information about all command and command id:s
        ///     Is used to correctly deserialize the result messages from commands.
        /// </summary>
        private readonly ConcurrentDictionary<int, CommandMessage> _commandsSentAndResponseShouldBeDisregarded =
            new(32, 200);

        /// <summary>
        ///     Default Json serialization options, Hass expects intended
        /// </summary>
        private readonly JsonSerializerOptions _defaultSerializerOptions = new()
        {
            WriteIndented = false,
            IgnoreNullValues = true
        };

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private readonly IClientWebSocketFactory _wsFactory;
        private readonly ITransportPipelineFactory<HassMessage>? _pipelineFactory;
        private readonly HttpClient _httpClient;

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
        ///     Channel used as a async thread safe way to read result messages from the websocket
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
        private IClientWebSocket? _ws;

        public HassClient(ILoggerFactory? loggerFactory = null) : this(
            loggerFactory ?? LoggerHelper.CreateDefaultLoggerFactory(),
            WebSocketHelper.CreatePipelineFactory(),
            WebSocketHelper.CreateClientFactory(),
            HttpHelper.CreateHttpClient())
        {
        }

        /// <summary>
        ///     Instance a new HassClient
        /// </summary>
        /// <param name="loggerFactory">The LogFactory to use for logging, null uses default values from config.</param>
        /// <param name="pipelineFactory"></param>
        /// <param name="wsFactory">The factory to use for websockets, mainly for testing purposes</param>
        /// <param name="httpClient">The client responsible for handling HTTP-requests.</param>
        public HassClient(
            ILoggerFactory loggerFactory,
            ITransportPipelineFactory<HassMessage> pipelineFactory,
            IClientWebSocketFactory wsFactory,
            HttpClient httpClient)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<HassClient>();

            _pipelineFactory = pipelineFactory;
            _wsFactory = wsFactory;
            _httpClient = httpClient;
        }

        /// <summary>
        ///     The current states of the entities.
        /// </summary>
        public ConcurrentDictionary<string, HassState> States { get; } =
            new(Environment.ProcessorCount * 2, DefaultChannelSize);

        /// <summary>
        ///     Internal property for tests to access the timeout during unit testing
        /// </summary>
        internal int SocketTimeout { get; set; } = DefaultTimeout;

        // /// <inheritdoc/>
        // public async Task<bool> CallService(string domain, string service, object serviceData, bool waitForResponse = true) =>
        //     await CallService(domain, service, serviceData, null, waitForResponse);

        /// <inheritdoc/>
        public async Task<bool> CallService(string domain, string service, object? serviceData = null, HassTarget? target = null, bool waitForResponse = true)
        {
            try
            {
                HassMessage result = await SendCommandAndWaitForResponse(new CallServiceCommand
                {
                    Domain = domain,
                    Service = service,
                    ServiceData = serviceData,
                    Target = target
                }, waitForResponse).ConfigureAwait(false);
                return result.Success ?? false;
            }
            catch (OperationCanceledException) when (!CancelSource.IsCancellationRequested)
            {
                return false; // Just timeout not canceled
            }
        }

        /// <summary>
        ///     Closes the websocket
        /// </summary>
        public async Task CloseAsync()
        {
            lock (States)
            {
                if (_isClosing || _ws == null || _isClosed)
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
                using var timeout = new CancellationTokenSource(MaxWaitTimeSocketClose);

                // This closes the underlying websocket as well
                if (_messagePipeline is object)
                    await _messagePipeline.CloseAsync().ConfigureAwait(false);

                try
                {
                    if (
                        _ws.State == WebSocketState.Open ||
                        _ws.State == WebSocketState.CloseReceived ||
                        _ws.State == WebSocketState.CloseSent
                        )
                    {
                        _logger.LogWarning("Unexpected state, Expected closed, got {state}", _ws.State);
                    }
                }
                catch (OperationCanceledException)
                {
                    // normal upon task/token cancellation, disregard
                }

                // Cancel all async stuff
                CancelSource.Cancel();

                // Wait for read and write tasks to complete max 5 seconds
                if (_readMessagePumpTask is object)
                {
                    await _readMessagePumpTask.ConfigureAwait(false);
                }
            }
            finally
            {
                _ws?.Dispose();
                _ws = null;

                if (_messagePipeline is object)
                    await _messagePipeline.DisposeAsync().ConfigureAwait(false);

                _messagePipeline = null;

                CancelSource?.Dispose();
                CancelSource = new CancellationTokenSource();

                _isClosed = true;
                _isClosing = false;

                _logger.LogTrace("Async close websocket done");
            }
        }

        //// <inheritdoc/>
        public Task<bool> ConnectAsync(string host, short port, bool ssl, string token, bool getStatesOnConnect) =>
            ConnectAsync(new Uri($"{(ssl ? "wss" : "ws")}://{host}:{port}/api/websocket"), token, getStatesOnConnect);

        /// <inheritdoc/>
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

            // Setup default headers for httpClient
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

                    // Do the authenticate and get the authorization response
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
                _logger.LogDebug(e, $"Failed to connect to Home Assistant on {url}");
            }

            return false;
        }

        /// <inheritdoc/>
        public async Task<HassConfig> GetConfig()
        {
            HassMessage hassResult = await SendCommandAndWaitForResponse(new GetConfigCommand()).ConfigureAwait(false);

            object resultMessage =
                hassResult.Result ?? throw new ApplicationException("Unexpected response from command");

            if (resultMessage is HassConfig result)
            {
                return result;
            }

            throw new ApplicationException($"The result not expected! {resultMessage}");
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task<HassEvent> ReadEventAsync() => await _eventChannel.Reader.ReadAsync(CancelSource.Token).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task<HassEvent> ReadEventAsync(CancellationToken token)
        {
            using var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(CancelSource.Token, token);
            return await _eventChannel.Reader.ReadAsync(cancelSource.Token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> SendEvent(string eventId, object? data = null)
        {
            var apiUrl = $"{_apiUrl}/events/{HttpUtility.UrlEncode(eventId)}";
            var content = "";

            if (data != null)
            {
                content = JsonSerializer.Serialize(data, _defaultSerializerOptions);
            }

            try
            {
                using var sc = new StringContent(content, Encoding.UTF8);

                var result = await _httpClient.PostAsync(new Uri(apiUrl),
                    sc,
                    CancelSource.Token).ConfigureAwait(false);

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

        /// <inheritdoc/>
        public async Task<T?> PostApiCall<T>(string apiPath, object? data = null)
        {
            var apiUrl = $"{_apiUrl}/{apiPath}";
            var content = "";

            try
            {
                if (data != null)
                {
                    content = JsonSerializer.Serialize(data, _defaultSerializerOptions);
                }
                using var sc = new StringContent(content, Encoding.UTF8);

                if (content.Length > 0)
                {
                    sc.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");
                }

                var result = await _httpClient.PostAsync(new Uri(apiUrl),
                    sc,
                    CancelSource.Token).ConfigureAwait(false);

                if (result.IsSuccessStatusCode)
                {
                    if (result.Content.Headers.ContentLength > 0)
                    {
                        return await JsonSerializer.DeserializeAsync<T>(result.Content.ReadAsStream(), null, CancelSource.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        return default;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to post api");
            }
            return default;
        }

        /// <inheritdoc/>
        public async Task<T?> GetApiCall<T>(string apiPath)
        {
            var apiUrl = $"{_apiUrl}/{apiPath}";

            try
            {
                var result = await _httpClient.GetAsync(new Uri(apiUrl),
                    CancelSource.Token).ConfigureAwait(false);

                if (result.IsSuccessStatusCode)
                {
                    return await JsonSerializer.DeserializeAsync<T>(result.Content.ReadAsStream(), null, CancelSource.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get api");
            }
            return default;
        }

        /// <inheritdoc/>
        public async Task<HassState?> SetState(string entityId, string state, object? attributes)
        {
            var apiUrl = $"{_apiUrl}/states/{HttpUtility.UrlEncode(entityId)}";
            var content = JsonSerializer.Serialize<object>(
                    new { state, attributes }, _defaultSerializerOptions);

            try
            {
                using var sc = new StringContent(content, Encoding.UTF8);
                var result = await _httpClient.PostAsync(new Uri(apiUrl),
                    sc,
                    CancelSource.Token).ConfigureAwait(false);

                if (result.IsSuccessStatusCode)
                {
                    return await JsonSerializer.DeserializeAsync<HassState>(await result.Content.ReadAsStreamAsync().ConfigureAwait(false),
                        _defaultSerializerOptions).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to set state on {entity} with state {state}", entityId, state);
            }
            return null;
        }

        /// <inheritdoc/>
        public async Task<HassState?> GetState(string entityId)
        {
            var apiUrl = $"{_apiUrl}/states/{HttpUtility.UrlEncode(entityId)}";

            try
            {
                var result = await _httpClient.GetAsync(new Uri(apiUrl),
                    CancelSource.Token).ConfigureAwait(false);

                if (result.IsSuccessStatusCode)
                {
                    return await JsonSerializer.DeserializeAsync<HassState>(await result.Content.ReadAsStreamAsync().ConfigureAwait(false),
                        _defaultSerializerOptions).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get state on {entity}", entityId);
            }
            return null;
        }

        public async Task<bool> SubscribeToEvents(EventType eventType = EventType.All)
        {
            if (eventType != EventType.All)
            {
                var command = new SubscribeEventCommand()
                {
                    EventType = eventType switch
                    {
                        EventType.HomeAssistantStart => "homeassistant_start",
                        EventType.HomeAssistantStop => "homeassistant_stop",
                        EventType.StateChanged => "state_changed",
                        EventType.ServiceRegistered => "service_registered",
                        EventType.CallService => "call_service",
                        EventType.ServiceExecuted => "service_executed",
                        EventType.PlatformDiscovered => "platform_discovered",
                        EventType.ComponentLoaded => "component_loaded",
                        EventType.TimeChanged => "time_changed",
                        EventType.AutomationReloaded => "automation_reloaded",
                        EventType.SceneReloaded => "scene_reloaded",
                        _ => throw new ArgumentException($"Event type {eventType} not supported", nameof(eventType))
                    }
                };
                var result = await SendCommandAndWaitForResponse(command).ConfigureAwait(false);
                return result.Success ?? false;
            }
            else
            {
                var command = new SubscribeEventCommand();
                var result = await SendCommandAndWaitForResponse(command).ConfigureAwait(false);
                return result.Success ?? false;
            }
        }

        public async Task<IEnumerable<HassServiceDomain>> GetServices()
        {
            HassMessage servicesResult = await SendCommandAndWaitForResponse(new GetServicesCommand()).ConfigureAwait(false);

            return servicesResult.Result as IEnumerable<HassServiceDomain>
                    ?? throw new ApplicationException("Unexpected response from GetServices command");
        }

        public async Task<IReadOnlyCollection<HassArea>> GetAreas()
        {
            HassMessage servicesResult = await SendCommandAndWaitForResponse(new GetAreasCommand()).ConfigureAwait(false);

            return servicesResult.Result as IReadOnlyCollection<HassArea>
                    ?? throw new ApplicationException("Unexpected response from GetServices command");
        }

        public async Task<IReadOnlyCollection<HassDevice>> GetDevices()
        {
            HassMessage devicesResult = await SendCommandAndWaitForResponse(new GetDevicesCommand()).ConfigureAwait(false);

            return devicesResult.Result as IReadOnlyCollection<HassDevice>
                    ?? throw new ApplicationException("Unexpected response from GetDevices command");
        }

        public async Task<IReadOnlyCollection<HassEntity>> GetEntities()
        {
            HassMessage servicesResult = await SendCommandAndWaitForResponse(new GetEntitiesCommand()).ConfigureAwait(false);

            return servicesResult.Result as IReadOnlyCollection<HassEntity>
                    ?? throw new ApplicationException("Unexpected response from GetServices command");
        }

        /// <summary>
        ///     Process next message from Home Assistant
        /// </summary>
        /// <remarks>
        ///     Uses Pipes to allocate memory where the websocket writes to and
        ///     Write the read message to a channel.
        /// </remarks>
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
                        if (m?.Event != null)
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
                        _logger.LogDebug($"Unexpected event type {m?.Type}, discarding message!");
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

        /// <summary>
        ///     Sends a command message and wait for result
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="waitForResponse">true if it wait for response</param>
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
                commandMessage.Id = Interlocked.Increment(ref _messageId);
                if (waitForResponse)
                {
                    //We save the type of command so we can deserialize the correct message later
                    _commandsSent[_messageId] = commandMessage;
                }
                else
                {
                    _commandsSentAndResponseShouldBeDisregarded[_messageId] = commandMessage;
                }
            }

            return _messagePipeline.SendMessageAsync(message, CancelSource.Token);
        }

        private static string FormatCommand(CommandMessage message)
        {
            return message switch
            {
                CallServiceCommand cc => $"call_service: {cc.Domain}.{cc.Service} [{cc.ServiceData}] ({cc.Target})",
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
                if (_commandsSent.TryRemove(m.Id, out CommandMessage? command))
                {
                    if (m.Success == false && command is object)
                    {
                        _logger.LogWarning($"Non successful result for command {FormatCommand(command)}: code({m.Error?.Code}), message: {m.Error?.Message}");
                    }
                    switch (command?.Type)
                    {
                        case "get_states":
                            m.Result = m.ResultElement?.ToObject<IReadOnlyCollection<HassState>>(_defaultSerializerOptions);
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
                            m.Result = m.ResultElement?.ToObject<IReadOnlyCollection<HassArea>>(_defaultSerializerOptions);
                            break;
                        case "config/device_registry/list":
                            m.Result = m.ResultElement?.ToObject<IReadOnlyCollection<HassDevice>>(_defaultSerializerOptions);
                            break;
                        case "config/entity_registry/list":
                            m.Result = m.ResultElement?.ToObject<IReadOnlyCollection<HassEntity>>(_defaultSerializerOptions);
                            break;
                        default:
                            _logger.LogError($"The command message {command?.Type} is not supported");
                            break;
                    }
                }
                else
                {
                    var resultMsg = _commandsSentAndResponseShouldBeDisregarded.TryRemove(m.Id, out CommandMessage? originalCommand) ? null : m;

                    if (m.Success == false && resultMsg is object && originalCommand is object)
                    {
                        _logger.LogWarning($"Non successful unwaited result for command {FormatCommand(originalCommand)}: code({m.Error?.Code}), message: {m.Error?.Message}");
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
                return wsResult;
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

        /// <inheritdoc/>
        public async Task TriggerWebhook(string id, object? data)
        {
            var encodedId = HttpUtility.UrlEncode(id);
            await PostApiCall<object>($"webhook/{encodedId}", data).ConfigureAwait(false);
        }
    }
}