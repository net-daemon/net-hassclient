using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace HassClient.Unit.Tests
{
    class WSClientMock : IWsClient
    {
        public bool CloseIsRun { get; set; } = false;

        public async Task CloseAsync()
        {
            CloseIsRun = true;
            await Task.Delay(2);
        }

        public async Task<bool> ConnectAsync(Uri url)
        {
            if (url.AbsoluteUri != "ws://localhost:8192/api/websocket")
                return false;
            return true;
        }

        public async Task<HassMessage> ReadMessageAsync()
        {
            throw new NotImplementedException();
        }

        public bool SendMessage(MessageBase message)
        {
            throw new NotImplementedException();
        }
    }
}
