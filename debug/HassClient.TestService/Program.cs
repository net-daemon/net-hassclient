using System;
using Microsoft.Extensions.Hosting;
using JoySoftware.HomeAssistant.Extensions;
using Microsoft.Extensions.Configuration;
using JoySoftware.HomeAssistant.Client.TestService;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CA1812

try
{
    await Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            // services.Configure<HomeAssistantSettings>(context.Configuration.GetSection("HomeAssistant"));
            services.Configure<HomeAssistantSettings>(context.Configuration.GetSection("HomeAssistant"));
            services.AddHostedService<DebugService>();
            services.AddHassClient();
            services.AddSingleton(context.Configuration);
        })
        .Build()
        .RunAsync()
        .ConfigureAwait(false);
}
catch (Exception e)
{
    Console.WriteLine($"Failed to start host... {e}");
    throw;
}
finally
{

}