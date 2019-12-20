namespace HassClient.Unit.Tests
{
    public class HassClientTests
    {
        //[Fact]
        //public async void TestGoodConnect()
        //{
        //    // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
        //    var hc = new HassClient(new WSClientMock());
        //    Assert.True(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket")));

        //}

        //[Fact]
        //public async void TestFailedConnect()
        //{
        //    // Simulate an fail connect by not using the "ws://localhost:8192/api/websocket" address
        //    var hc = new HassClient(new WSClientMock());
        //    Assert.False(await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket_fail")));


        //}
        //[Fact]
        //public async void TestConnectNullUri()
        //{
        //    // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
        //    var hc = new HassClient(new WSClientMock());
        //    await Assert.ThrowsAsync<ArgumentNullException>(async () => await hc.ConnectAsync(null));

        //}
        //[Fact]
        //public async void TestClose()
        //{
        //    var mock = new WSClientMock();
        //    // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
        //    var hc = new HassClient(mock);
        //    await hc.ConnectAsync(new Uri("ws://localhost:8192/api/websocket"));
        //    // Then disconnect 
        //    await hc.CloseAsync();
        //    Assert.True(mock.CloseIsRun);
        //}

        //[Fact]
        //public async void TestCloseNull()
        //{
        //    // Simulate an ok connect by using the "ws://localhost:8192/api/websocket" address
        //    var hc = new HassClient(new WSClientMock());
        //    await Assert.ThrowsAsync<ArgumentNullException>(async () => await hc.ConnectAsync(null));

        //}

        ////[Fact]
        //public async void TestReadEventMessage()
        //{
        //    var mock = new WSClientMock();
        //    var hc = new HassClient(mock);
        //    var msgIn = new HassMessage()
        //    {
        //        Id = 1,
        //        Type = "event",
        //        Event = new EventMessage()
        //        {
        //            TimeFired = DateTime.UtcNow,
        //            Data = new StateChangedEventMessage()
        //            {
        //                OldState = new StateMessage()
        //                {
        //                    State = "on",
        //                }
        //            }
        //        }
        //    };

        //    mock.Messages.Enqueue(msgIn);

        //    var message = await hc.ReadEventAsync();
        //    //           Assert.Equal(msgIn.Id, message.Id);
        //}
    }
}
