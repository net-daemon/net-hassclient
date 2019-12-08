using System;
using System.Threading.Tasks;
using Xunit;
namespace NetDaemonLibTest
{
    public class UnitTest1 : IClassFixture<HomeAssistantMockFixture>
    {
        HomeAssistantMockFixture mockFixture;

        public UnitTest1(HomeAssistantMockFixture fixture)
        {
            mockFixture = fixture;
        }

        [Fact]
        async public void Test1()
        {

            Assert.False(false);
            await Task.Delay(40000);

        }

        [Fact]
        public void Test2()
        {
            Assert.True(true);

        }
    }

    public class HomeAssistantMockFixture : IDisposable
    {
        HomeAssistantMock mock;
        public HomeAssistantMockFixture()
        {
            mock = new HomeAssistantMock();
        }
        public void Dispose()
        {
            mock.Stop();
        }
    }
}