using JoySoftware.HomeAssistant.Client;
using Moq;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace HassClient.Unit.Tests
{
    /// <summary>
    ///     The mock to use instead of the real underlying websocket for unit testing
    /// </summary>
    /// <remarks>
    ///     Add the json messages to be returned by the mock in the <see cref="ResponseMessages" /> Channel
    /// </remarks>
    class HassWebSocketMock : Mock<IClientWebSocket>
    {
        public static readonly string MessageFixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Messages");
        private int _currentReadPosition;
        private int _nrOfSentMessages;

        public HassWebSocketMock()
        {
            // Setup standard mock functionality

            // Do nothing to fake a good connect
            Setup(x => x.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(2));

            // Do nothing to fake a good close
            Setup(x =>
                    x.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    CloseStatus = WebSocketCloseStatus.NormalClosure;
                    State = WebSocketState.Closed;

                    return Task.Delay(2);
                });

            // Just fake good result as default
            Setup(x => x.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask());

            // Return the next message in ResponseMessages as default behaviour
            Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                .Returns(
                    async (Memory<byte> buffer, CancellationToken token) =>
                    {
                        var msgToSendBackToClient =
                            await ResponseMessages.Reader.ReadAsync(token).ConfigureAwait(false);
                        _nrOfSentMessages++;
                        return HandleResult(msgToSendBackToClient, buffer);
                    });

            // Set Open state as default, special tests for closed states
            SetupGet(x => x.State).Returns(WebSocketState.Open);

            WebSocketMockFactory.Setup(n => n.New()).Returns(() => this.Object);
        }

        public static string StateMessage => File.ReadAllText(Path.Combine(MessageFixturePath, "result_states.json"));
        public static string ConfigMessage => File.ReadAllText(Path.Combine(MessageFixturePath, "result_config.json"));
        public static string EventMessage => File.ReadAllText(Path.Combine(MessageFixturePath, "event.json"));
        public static string ServiceMessage => File.ReadAllText(Path.Combine(MessageFixturePath, "service_event.json"));

        public WebSocketState State { get; set; }

        public WebSocketCloseStatus? CloseStatus { get; set; }

        public Channel<byte[]> ResponseMessages { get; } = Channel.CreateBounded<byte[]>(10);

        public LoggerMock Logger { get; } = new LoggerMock();

        public Mock<IClientWebSocketFactory> WebSocketMockFactory { get; } = new Mock<IClientWebSocketFactory>();

        /// <summary>
        ///     Fakes a response from home assistant
        /// </summary>
        /// <remarks>
        ///     Takes next message from channel
        /// </remarks>
        /// <param name="msg"></param>
        /// <param name="buffer"></param>
        private ValueWebSocketReceiveResult HandleResult(byte[] msg, Memory<byte> buffer)
        {
            if ((msg.Length - _currentReadPosition) > buffer.Length)
            {
                msg.AsMemory(_currentReadPosition, buffer.Length).CopyTo(buffer);
                _currentReadPosition += buffer.Length;
                // Re-enter the message type in channel cause it is a continuous message
                ResponseMessages.Writer.TryWrite(msg);
                return new ValueWebSocketReceiveResult(
                    buffer.Length, WebSocketMessageType.Text, false);
            }

            int len = msg.Length - _currentReadPosition;
            msg.AsMemory(_currentReadPosition, len).CopyTo(buffer);

            _currentReadPosition = 0;
            return new ValueWebSocketReceiveResult(
                len, WebSocketMessageType.Text, true);
        }

        /// <summary>
        ///     Adds a fake response json message that fakes the home assistant server response
        /// </summary>
        /// <param name="message">Message to fake</param>
        public void AddResponse(string message)
        {
            ResponseMessages.Writer.TryWrite(Encoding.UTF8.GetBytes(message));
        }

        /// <summary>
        ///     Gets the HassClient setup with default fakes
        /// </summary>
        /// <returns></returns>
        public JoySoftware.HomeAssistant.Client.HassClient GetHassClient()
        {
            return new JoySoftware.HomeAssistant.Client.HassClient(Logger.LoggerFactory, WebSocketMockFactory.Object);
        }

        /// <summary>
        ///     Returns a HassClient that has authorize messages default but not connected
        /// </summary>
        public JoySoftware.HomeAssistant.Client.HassClient GetHassClientNotConnected()
        {
            var hassClient = GetHassClient();
            // First message from Home Assistant is auth required
            AddResponse(@"{""type"": ""auth_required""}");
            // Next one we fake it is auth ok
            AddResponse(@"{""type"": ""auth_ok""}");

            return hassClient;
        }

        /// <summary>
        ///     Returns a connected HassClient to use for testing after connection phase
        /// </summary>
        /// <returns></returns>
        public async Task<JoySoftware.HomeAssistant.Client.HassClient> GetHassConnectedClient(bool getStates = false)
        {
            var hassClient = GetHassClient();
            // First message from Home Assistant is auth required
            AddResponse(@"{""type"": ""auth_required""}");
            // Next one we fake it is auth ok
            AddResponse(@"{""type"": ""auth_ok""}");

            await hassClient.ConnectAsync(new Uri("ws://anyurldoesntmatter.org"), "FAKETOKEN", getStates);

            return hassClient;
        }

        /// <summary>
        ///     Waits until the fake websocket is connected
        /// </summary>
        /// <remarks>
        ///     This is used to test other messages that requires the connection to be up
        /// </remarks>
        /// <returns></returns>
        public async Task WaitUntilConnected()
        {
            while (_nrOfSentMessages < 2)
                await Task.Delay(20);

            await Task.Delay(20);
        }
    }
}