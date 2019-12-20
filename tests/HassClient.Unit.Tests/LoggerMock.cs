using Microsoft.Extensions.Logging;
using System;

namespace HassClient.Unit.Tests
{
    class LoggerMock : ILogger
    {
        public bool LoggedError { get; internal set; } = false;
        public bool LoggedTrace { get; internal set; } = false;

        public IDisposable BeginScope<TState>(TState state) => throw new NotImplementedException();
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Trace)
                LoggedTrace = true;
            if (logLevel == LogLevel.Error)
                LoggedError = true;
        }
    }
    class LoggerFactoryMock : ILoggerFactory
    {
        public bool LoggedError { get; internal set; }
        public bool LoggedTrace { get; internal set; }

        public void AddProvider(ILoggerProvider provider) => throw new NotImplementedException();
        public ILogger CreateLogger(string categoryName) => new LoggerMock();
        public void Dispose() => throw new NotImplementedException();
    }
}
