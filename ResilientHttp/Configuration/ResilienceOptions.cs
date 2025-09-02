using System;
using System.Net;

namespace resilient_http_client.ResilientHttp.Configuration;

public sealed class ResilienceOptions
{
    public int RetryCount { get; init; } = 3;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan? Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public Func<HttpStatusCode, bool>? AdditionalHttpRetry { get; init; }
}


