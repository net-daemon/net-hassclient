using JoySoftware.HomeAssistant.Client;
using JoySoftware.HomeAssistant.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace JoySoftware.HomeAssistant.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHassClient(this IServiceCollection services)
        {
            return services
                .AddClientFactory()
                .AddPipelineFactory()
                .AddHttpFactory()
                .AddHassClientImpl();
        }

        private static IServiceCollection AddHassClientImpl(this IServiceCollection services)
        {
            services.TryAddTransient<IHassClient>(provider =>
            {
                var loggerFactory = provider.GetService<ILoggerFactory>() ?? LoggerHelper.CreateDefaultLoggerFactory();
                var pipelineFactory = provider.GetRequiredService<ITransportPipelineFactory<HassMessage>>();
                var clientFactory = provider.GetRequiredService<IClientWebSocketFactory>();
                var httpFactory = provider.GetRequiredService<IHttpClientFactory>();

                return new HassClient(loggerFactory, pipelineFactory, clientFactory, httpFactory.CreateClient());
            });

            return services;
        }

        private static IServiceCollection AddClientFactory(this IServiceCollection services)
        {
            services.TryAddTransient<IClientWebSocketFactory, ClientWebSocketFactory>();
            return services;
        }

        private static IServiceCollection AddPipelineFactory(this IServiceCollection services)
        {
            services.TryAddTransient<ITransportPipelineFactory<HassMessage>, WebSocketMessagePipelineFactory<HassMessage>>();
            return services;
        }

        private static IServiceCollection AddHttpFactory(this IServiceCollection services)
        {
            services.AddHttpClient<IHassClient, HassClient>().ConfigurePrimaryHttpMessageHandler(ConfigureHttpMessageHandler);
            return services;
        }

        private static HttpMessageHandler ConfigureHttpMessageHandler(IServiceProvider provider)
        {
            var handler = provider.GetService<HttpMessageHandler>();
            return handler ?? HttpHelper.CreateHttpMessageHandler();
        }
    }
}