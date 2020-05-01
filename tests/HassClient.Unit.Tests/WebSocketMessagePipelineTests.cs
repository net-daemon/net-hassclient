using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JoySoftware.HomeAssistant.Client;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
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