using Microsoft.Extensions.DependencyInjection;
using resilient_http_client.ResilientHttp.Abstractions;
using resilient_http_client.ResilientHttp.Configuration;

namespace resilient_http_client.ResilientHttp.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResilientHttpClient(this IServiceCollection services, Action<ResilienceOptions>? configure = null)
    {
        var options = new ResilienceOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<IResilientHttpClient, FlurlPollyResilientHttpClient>();
        return services;
    }
}


