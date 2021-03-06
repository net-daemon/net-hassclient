﻿using JoySoftware.HomeAssistant.Client;
using JoySoftware.HomeAssistant.Messages;

namespace JoySoftware.HomeAssistant.Helpers
{
    internal static class WebSocketHelper
    {
        public static ITransportPipelineFactory<HassMessage> CreatePipelineFactory()
        {
            return new WebSocketMessagePipelineFactory<HassMessage>();
        }

        public static IClientWebSocketFactory CreateClientFactory()
        {
            return new ClientWebSocketFactory();
        }
    }
}