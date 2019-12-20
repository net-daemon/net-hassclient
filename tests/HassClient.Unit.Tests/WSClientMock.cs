using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace HassClient.Unit.Tests
{
    internal class WSClientMock : IWsClient
    {
        public bool CloseIsRun { get; set; } = false;
        public Queue<HassMessage> Messages { get; set; } = new Queue<HassMessage>();

        public async Task CloseAsync()
        {
            CloseIsRun = true;
            await Task.Delay(2);
        }

        public async Task<bool> ConnectAsync(Uri url)
        {
            if (url.AbsoluteUri != "ws://localhost:8192/api/websocket")
            {
                throw new WebSocketException("Fail to connect.");
            }

            return true;
        }

        public async Task<HassMessage> ReadMessageAsync()
        {
            return Messages.Dequeue();
        }

        public bool SendMessage(MessageBase message) => throw new NotImplementedException();
    }
}
