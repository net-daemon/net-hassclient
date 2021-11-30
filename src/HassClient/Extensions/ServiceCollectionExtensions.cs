using JoySoftware.HomeAssistant.Client;
using JoySoftware.HomeAssistant.Helpers;
using JoySoftware.HomeAssistant.Messages;
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
                .AddWebSocketFactory()
                .AddPipelineFactory()
                .AddHttpClientAndFactory()
                .AddHassClientImpl();
        }

        private static IServiceCollection AddHassClientImpl(this IServiceCollection services)
        {
            services.AddSingleton<HassClient>();
            services.AddSingleton<IHassClient>(s => s.GetRequiredService<HassClient>());
            // Adds observable so we can inject IObservable<HassEvent>
            services.AddSingleton(
                s => s.GetRequiredService<IHassClient>().HassEventsObservable
                );
            return services;
        }

        private static IServiceCollection AddWebSocketFactory(this IServiceCollection services)
        {
            services.TryAddTransient<IClientWebSocketFactory, ClientWebSocketFactory>();
            return services;
        }

        private static IServiceCollection AddPipelineFactory(this IServiceCollection services)
        {
            services.TryAddTransient<ITransportPipelineFactory<HassMessage>, WebSocketMessagePipelineFactory<HassMessage>>();
            return services;
        }

        private static IServiceCollection AddHttpClientAndFactory(this IServiceCollection services)
        {
            services.AddSingleton(s => s.GetRequiredService<IHttpClientFactory>().CreateClient());
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