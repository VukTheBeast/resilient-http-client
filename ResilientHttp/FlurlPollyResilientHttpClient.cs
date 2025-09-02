using Flurl.Http;
using Polly;
using resilient_http_client.ResilientHttp.Abstractions;
using resilient_http_client.ResilientHttp.Configuration;
using resilient_http_client.ResilientHttp.Policies;

namespace resilient_http_client.ResilientHttp;

internal sealed class FlurlPollyResilientHttpClient : IResilientHttpClient
{
    private readonly ResilienceOptions _options;
    private readonly IFlurlClient _client;

    public FlurlPollyResilientHttpClient(ResilienceOptions options)
    {
        _options = options;
        _client = new FlurlClient(CreateHttpClientWithPolicies());
    }

    public async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken = default)
        => await CreateRequest(url)
            .GetJsonAsync<T>(cancellationToken: cancellationToken);

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
        => await CreateRequest(url)
            .GetStringAsync(cancellationToken: cancellationToken);

    public async Task<TResponse> PostJsonAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default)
        => await CreateRequest(url)
            .PostJsonAsync(body, cancellationToken: cancellationToken)
            .ReceiveJson<TResponse>();

    public async Task<TResponse> PutJsonAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default)
        => await CreateRequest(url)
            .PutJsonAsync(body, cancellationToken: cancellationToken)
            .ReceiveJson<TResponse>();

    public async Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        await CreateRequest(url)
            .DeleteAsync(cancellationToken: cancellationToken);
    }

    private IFlurlRequest CreateRequest(string url)
    {
        if (_options.Timeout.HasValue)
        {
            _client.Settings.Timeout = _options.Timeout.Value;
        }
        return _client.Request(url);
    }

    private HttpClient CreateHttpClientWithPolicies()
    {
        var retryPolicy = Polly.Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => PollyHttpUtilities.ShouldRetryStatusCode(r.StatusCode, _options))
            .WaitAndRetryAsync(_options.RetryCount,
                retryAttempt => TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1)));

        var handler = new PolicyDelegatingHandler(retryPolicy)
        {
            InnerHandler = new HttpClientHandler()
        };

        var httpClient = new HttpClient(handler);
        if (_options.Timeout.HasValue)
        {
            httpClient.Timeout = _options.Timeout.Value;
        }
        return httpClient;
    }
}


