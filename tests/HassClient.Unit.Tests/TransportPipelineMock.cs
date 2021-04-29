using JoySoftware.HomeAssistant.Client;
using JoySoftware.HomeAssistant.Messages;
using Moq;

namespace HassClient.Unit.Tests
{
    public class TransportPipelineMock : Mock<ITransportPipeline<HassMessage>>
    {


    }

    public class TransportPipelineFactoryMock : Mock<ITransportPipelineFactory<HassMessage>>
    {
        readonly HassWebSocketMock wsMock = new();
        readonly LoggerMock loggerMock = new();
        public TransportPipelineFactoryMock(IClientWebSocket client = null)
        {
            client ??= wsMock.Object;
            Setup(n => n.CreateWebSocketMessagePipeline(client, loggerMock.LoggerFactory));
        }

    }
}