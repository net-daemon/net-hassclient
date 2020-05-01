using JoySoftware.HomeAssistant.Client;
using Moq;

namespace HassClient.Unit.Tests
{
    public class TransportPipelineMock : Mock<ITransportPipeline<HassMessage>>
    {


    }

    public class TransportPipelineFactoryMock : Mock<ITransportPipelineFactory<HassMessage>>
    {
        TransportPipelineMock pipeline = new TransportPipelineMock();
        HassWebSocketMock wsMock = new HassWebSocketMock();

        LoggerMock loggerMock = new LoggerMock();
        public TransportPipelineFactoryMock(IClientWebSocket client = null)
        {
            client = client ?? wsMock.Object;
            Setup(n => n.CreateWebSocketMessagePipeline(client, loggerMock.LoggerFactory));
        }

    }
}