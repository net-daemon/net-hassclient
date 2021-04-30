using System;
using System.Text.Json;
using JoySoftware.HomeAssistant.Messages;
using JoySoftware.HomeAssistant.Model;
using Xunit;

namespace HassClient.Unit.Tests
{
    public class HassMessagesTests
    {
        [Fact]
        public void SerializeHassMessageShouldReturnException()
        {
            // ARRANGE
            var msg = new HassMessage {Id = 1, Success = true, Result = 1, Event = new HassEvent()};

            // ACT AND ASSERT
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(msg));
        }
    }
}