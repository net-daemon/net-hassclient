using System;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoySoftware.HomeAssistant.Client
{
    public interface ITransportPipeline<T> : IAsyncDisposable where T : class
    {
        /// <summary>
        ///     Gets next message from pipeline
        /// </summary>
        ValueTask<T> GetNextMessageAsync(CancellationToken cancellationToken);

        /// <summary>
        ///     Sends a message to the pipline
        /// </summary>
        /// <param name="message"></param>
        Task SendMessageAsync<T1>(T1 message, CancellationToken cancellationToken) where T1 : class;

        /// <summary>
        ///     Close the pipeline, it will also close the underlying websocket
        /// </summary>
        Task CloseAsync();

        /// <summary>
        ///  Returns true if the pipeline is accepting and receiving messages
        /// </summary>
        bool IsValid { get; }
    }

    public interface ITransportPipelineFactory<T> where T : class
    {
        ITransportPipeline<T> CreateWebSocketMessagePipeline(
            IClientWebSocket webSocketClient,
            ILoggerFactory? loggerFactory = null);


    }

    internal class WebSocketMessagePipelineFactory<T> : ITransportPipelineFactory<T> where T : class
    {
        public ITransportPipeline<T> CreateWebSocketMessagePipeline(IClientWebSocket webSocketClient, ILoggerFactory? loggerFactory = null)
        {
            return WebSocketMessagePipeline<T>.CreateWebSocketMessagePipeline(webSocketClient, loggerFactory);
        }
    }

    internal class WebSocketMessagePipeline<T> : ITransportPipeline<T> where T : class
    {
        private readonly ILogger<WebSocketMessagePipeline<T>> _logger;

        private readonly IClientWebSocket _ws;
        private readonly Task _readMessagePumpTask;
        private readonly Task _writeMessagePumpTask;

        // Used on DisposeAsync to make sure the tasks are ended
        private readonly CancellationTokenSource _internalCancellationSource = new CancellationTokenSource();
        private readonly CancellationToken _internalCancelToken;

        private readonly Pipe _pipe = new Pipe();

        /// <summary>
        ///     The max time we will wait for the socket to gracefully close
        /// </summary>
        private const int MaxWaitTimeSocketClose = 5000; // 5 seconds

        /// <summary>
        ///     Default size for channel
        /// </summary>
        private const int DefaultChannelSize = 200;

        /// <summary>
        ///     Channel of the messages read
        /// </summary>
        private readonly Channel<T> _inChannel =
            Channel.CreateBounded<T>(DefaultChannelSize);

        /// <summary>
        ///     Channel of the messages sent
        /// </summary>
        private readonly Channel<object> _outChannel =
            Channel.CreateBounded<object>(DefaultChannelSize);

        /// <summary>
        ///     Default Json serialization options, Hass expects intended
        /// </summary>
        private readonly JsonSerializerOptions _defaultSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            IgnoreNullValues = true
        };

        /// <summary>
        ///     Check if this pipe is still valid, like message pumps still going and valid state of websocket
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (_readMessagePumpTask is object && _readMessagePumpTask.IsCompleted)
                    return false;

                if (_writeMessagePumpTask is object && _writeMessagePumpTask.IsCompleted)
                    return false;

                return _ws.State switch
                {
                    WebSocketState.Open => true,
                    WebSocketState.CloseReceived => true,
                    _ => false
                };
            }
        }

        public async ValueTask<T> GetNextMessageAsync(CancellationToken cancellationToken)
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_internalCancelToken, cancellationToken);
            return await _inChannel.Reader.ReadAsync(linkedTokenSource.Token).ConfigureAwait(false);
        }

        public async Task SendMessageAsync<T1>(T1 message, CancellationToken cancellationToken) where T1 : class
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _internalCancelToken);

            await _outChannel.Writer.WriteAsync(message, linkedTokenSource.Token).ConfigureAwait(false);
        }

        public WebSocketMessagePipeline(IClientWebSocket webSocketClient, ILoggerFactory? loggerFactory = null)
        {
            if (webSocketClient.State != WebSocketState.Open)
                throw new ApplicationException($"Websocket state is not open, state is {webSocketClient.State}");

            _ws = webSocketClient;

            _internalCancelToken = _internalCancellationSource.Token;

            loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = loggerFactory.CreateLogger<WebSocketMessagePipeline<T>>();

            _readMessagePumpTask = ReadMessagePump();
            _writeMessagePumpTask = WriteMessagePump();
        }

        private async Task WriteMessagePump()
        {
            while (_ws != null && (!_internalCancelToken.IsCancellationRequested && !_ws.CloseStatus.HasValue))
            {
                try
                {
                    while (!_internalCancelToken.IsCancellationRequested)
                    {

                        var messageToSend = await _outChannel.Reader.ReadAsync(_internalCancelToken).ConfigureAwait(false);

                        if (_ws.State != WebSocketState.Open && _ws.State != WebSocketState.CloseReceived)
                        {
                            _logger.LogTrace("WriteMessagePump, state not Open or CloseReceived, exiting WriteMessagePump: {socketState}", _ws.State.ToString());
                            return;
                        }

                        byte[] result = JsonSerializer.SerializeToUtf8Bytes(messageToSend, messageToSend.GetType(),
                            _defaultSerializerOptions);

                        await _ws.SendAsync(result, WebSocketMessageType.Text, true, _internalCancelToken).ConfigureAwait(false);

                    }
                }
                catch (OperationCanceledException)
                {
                    // Canceled the thread just leave
                    break;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Major failure in WriteMessagePumpWriteMessagePump, exit...");
                    break;
                }
            }
        }

        private async Task ReadMessagePump()
        {
            while (_ws != null && (!_internalCancelToken.IsCancellationRequested && !_ws.CloseStatus.HasValue))
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
                finally
                {
                    // Always make sure the pipe is reset and ready to use next process message
                    _pipe.Reset();
                }
            }
        }

        private async Task SendCloseFrameToWebSocket()
        {
            var timeout = new CancellationTokenSource(MaxWaitTimeSocketClose);

            try
            {
                if (
                    _ws.State == WebSocketState.Open ||
                    _ws.State == WebSocketState.CloseReceived
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
        }

        /// <summary>
        ///     Reads and process next message from the websocket
        /// </summary>
        /// <returns></returns>
        private async Task ProcessNextMessage()
        {
            await Task.WhenAll(
               ReadFromClientSocket(),
               WriteMessagesToChannel()
           ).ConfigureAwait(false);

            async Task ReadFromClientSocket()
            {
                try
                {
                    while (_ws != null && (!_internalCancelToken.IsCancellationRequested && !_ws.CloseStatus.HasValue))
                    {
                        Memory<byte> memory = _pipe.Writer.GetMemory();

                        ValueWebSocketReceiveResult result = await _ws.ReceiveAsync(memory, _internalCancelToken).ConfigureAwait(false);

                        _internalCancelToken.ThrowIfCancellationRequested();

                        if (
                            _ws.State == WebSocketState.Open &&
                            result.MessageType != WebSocketMessageType.Close)
                        {
                            _pipe.Writer.Advance(result.Count);

                            await _pipe.Writer.FlushAsync(_internalCancelToken).ConfigureAwait(false);

                            if (result.EndOfMessage)
                            {
                                // We have successfully read the whole message, make available to reader (in finally block)
                                break;
                            }
                        }
                        else if (_ws.State == WebSocketState.CloseReceived)
                        {
                            // We got a close message from server or if it still open we got canceled
                            // in both cases it is important to send back the close message
                            await SendCloseFrameToWebSocket().ConfigureAwait(false);

                            // Cancel so the write thread is canceled before pipe is complete
                            _internalCancellationSource.Cancel();
                        }

                        // Continue reading next part of websocket message
                    }
                }
                catch (OperationCanceledException)
                {
                    // Canceled the thread just leave no errors to be found here
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Major failure in ReadFromClientSocket, exit...");
                    // Make sure we always cancel the other task of any reason
                    throw;
                }
                finally
                {
                    // Make sure the writer is completed so the Pipe can be reset even if fail
                    await _pipe.Writer.CompleteAsync().ConfigureAwait(false);
                }

            }

            // Task that deserializes the message and write the finished message to a channel
            async Task WriteMessagesToChannel()
            {
                try
                {
                    // ReSharper disable once AccessToDisposedClosure
                    T m = await JsonSerializer.DeserializeAsync<T>(_pipe.Reader.AsStream(),
                        cancellationToken: _internalCancelToken).ConfigureAwait(false);

                    if (!_inChannel.Writer.TryWrite(m))
                        throw new ApplicationException($"Failed to write to {nameof(_inChannel)}");
                }
                catch (OperationCanceledException)
                {
                    // Canceled the thread just leave
                }
                catch (JsonException jex)
                {
                    _logger.LogDebug(jex, "Error deserializing json ");
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Error deserializing and write message to channel");
                }
                finally
                {
                    // Always complete reader even the case of an error so it can be reset
                    await _pipe.Reader.CompleteAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task CloseAsync()
        {
            // Close the open websocket. We defenitely do not need it after close
            if (_ws.State == WebSocketState.CloseReceived || _ws.State == WebSocketState.Open)
            {
                // Maske sure we send back to acknowledge the close message to server
                await SendCloseFrameToWebSocket().ConfigureAwait(false);
            }

            // Cancel all activity and wait for complete
            _internalCancellationSource.Cancel();

            // Wait for the messagepumps to end
            await Task.WhenAll(
               _readMessagePumpTask,
               _writeMessagePumpTask
           ).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await CloseAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Error cleaning up the websocket pipeline");
            }
        }

        public static ITransportPipeline<T> CreateWebSocketMessagePipeline(
            IClientWebSocket webSocketClient,
            ILoggerFactory? loggerFactory = null)
        {
            return new WebSocketMessagePipeline<T>(webSocketClient, loggerFactory);
        }
    }

}