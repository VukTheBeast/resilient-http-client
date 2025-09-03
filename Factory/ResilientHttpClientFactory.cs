using ResilientHttp.Abstractions;
using ResilientHttp.Configuration;

namespace ResilientHttp.Factory;

public static class ResilientHttpClientFactory
{
    public static IResilientHttpClient Create(ResilienceOptions? options = null)
        => new FlurlPollyResilientHttpClient(options ?? new ResilienceOptions());
}


