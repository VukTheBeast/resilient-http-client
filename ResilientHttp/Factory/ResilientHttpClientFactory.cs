using resilient_http_client.ResilientHttp.Abstractions;
using resilient_http_client.ResilientHttp.Configuration;

namespace resilient_http_client.ResilientHttp.Factory;

public static class ResilientHttpClientFactory
{
    public static IResilientHttpClient Create(ResilienceOptions? options = null)
        => new FlurlPollyResilientHttpClient(options ?? new ResilienceOptions());
}


