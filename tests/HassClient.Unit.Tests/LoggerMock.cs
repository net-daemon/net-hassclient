using Microsoft.Extensions.Logging;
using Moq;
using System;

namespace HassClient.Unit.Tests
{
    class LoggerMock
    {
        private readonly Mock<ILoggerFactory> _mockLoggerFactory = new Mock<ILoggerFactory>();
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        public ILoggerFactory LoggerFactory => _mockLoggerFactory.Object;
        public Mock<ILoggerFactory> MockLoggerFactory => _mockLoggerFactory;

        public ILogger Logger => _mockLogger.Object;
        public Mock<ILogger> MockLogger => _mockLogger;


        public LoggerMock()
        {
            // Setup the mock
            _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
        }

        /// <summary>
        /// Assert if the log has been used at times
        /// </summary>
        /// <param name="level">The loglevel being checked</param>
        /// <param name="times">The Times it has been logged</param>
        public void AssertLogged(LogLevel level, Times times)
        {
            _mockLogger.Verify(
               x => x.Log(
                       level,
                       It.IsAny<EventId>(),
                       It.Is<It.IsAnyType>((v, t) => true),
                       It.IsAny<Exception>(),
                       It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), times);
        }
    }
}
