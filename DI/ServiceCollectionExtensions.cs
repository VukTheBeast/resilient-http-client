using Microsoft.Extensions.DependencyInjection;
using ResilientHttp.Abstractions;
using ResilientHttp.Configuration;

namespace ResilientHttp.DI;

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


