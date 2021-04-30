using System.Threading;
using System.Threading.Tasks;
using JoySoftware.HomeAssistant.Client;
using JoySoftware.HomeAssistant.Messages;
using Xunit;

namespace HassClient.Unit.Tests
{
    public class WebSocketMessagePipelineTests
    {
        [Fact]
        public async Task WrongMessagesFromHassShouldReturnFalse()
        {
            // ARRANGE
            var mock = new HassWebSocketMock();

            // First message from Home Assistant is auth required
            mock.AddResponse(@"{""type"": ""any_kind_of_type""}");

            await using var pipe = new WebSocketMessagePipeline<HassMessageBase>(mock.Object);

            // ACT
            var msg = await pipe.GetNextMessageAsync(CancellationToken.None);

            Assert.Equal("any_kind_of_type", msg.Type);
        }

    }
}