using Microsoft.Extensions.Logging;

namespace JoySoftware.HomeAssistant.Helpers
{
    internal static class LoggerHelper
    {
        public static ILoggerFactory CreateDefaultLoggerFactory() => LoggerFactory.Create(builder => builder
            .ClearProviders()
            .AddFilter("HassClient.HassClient", LogLevel.Information)
            .AddConsole()
        );
    }
}