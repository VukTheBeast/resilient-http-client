using Flurl.Http;
using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using resilient_http_client.ResilientHttp.Abstractions;
using resilient_http_client.ResilientHttp.Configuration;
using resilient_http_client.ResilientHttp.Policies;

namespace resilient_http_client.ResilientHttp;

internal sealed class FlurlPollyResilientHttpClient : IResilientHttpClient
{
    private readonly ResilienceOptions _options;
    private readonly IFlurlClient _client;
    private readonly ILogger<FlurlPollyResilientHttpClient>? _logger;

    public FlurlPollyResilientHttpClient(ResilienceOptions options, ILogger<FlurlPollyResilientHttpClient>? logger = null)
    {
        _options = options;
        _logger = logger;
        _client = new FlurlClient(CreateHttpClientWithPolicies());
    }

    public async Task<T> GetAsync<T>(string url, RequestOptions? options = null, CancellationToken cancellationToken = default)
        => await ExecuteWithPerCallPolicyAsync(
            () => CreateRequest(url, options).GetJsonAsync<T>(cancellationToken: cancellationToken),
            options,
            cancellationToken);

    public async Task<string> GetStringAsync(string url, RequestOptions? options = null, CancellationToken cancellationToken = default)
        => await ExecuteWithPerCallPolicyAsync(
            () => CreateRequest(url, options).GetStringAsync(cancellationToken: cancellationToken),
            options,
            cancellationToken);

    public async Task<TResponse> PostJsonAsync<TRequest, TResponse>(string url, TRequest body, RequestOptions? options = null, CancellationToken cancellationToken = default)
        => await ExecuteWithPerCallPolicyAsync(
            async () => await (await CreateRequest(url, options)
                .PostJsonAsync(body, cancellationToken: cancellationToken))
                .GetJsonAsync<TResponse>(),
            options,
            cancellationToken);

    public async Task<TResponse> PutJsonAsync<TRequest, TResponse>(string url, TRequest body, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default)
        => await ExecuteWithPerCallPolicyAsync(
            async () => await (await CreateRequest(url, options)
                .PutJsonAsync(body, cancellationToken: cancellationToken))
                .GetJsonAsync<TResponse>(),
            options,
            cancellationToken);

    public async Task DeleteAsync(string url, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        await ExecuteWithPerCallPolicyAsync(
            () => CreateRequest(url, options).DeleteAsync(cancellationToken: cancellationToken),
            options,
            cancellationToken);
    }

    private IFlurlRequest CreateRequest(string url, RequestOptions? options)
    {
        if (_options.Timeout.HasValue)
        {
            _client.Settings.Timeout = _options.Timeout.Value;
        }
        var req = _client.Request(url);

        if (options?.Headers != null)
        {
            req = req.WithHeaders(options.Headers);
        }
        if (!string.IsNullOrEmpty(options?.BearerToken))
        {
            req = req.WithOAuthBearerToken(options.BearerToken);
        }
        if (options?.BasicAuth != null)
        {
            req = req.WithBasicAuth(options.BasicAuth.Value.UserName, options.BasicAuth.Value.Password);
        }
        return req;
    }

    private HttpClient CreateHttpClientWithPolicies()
    {
        var delays = Backoff.DecorrelatedJitterBackoffV2(_options.BaseDelay, _options.RetryCount);
        var retryPolicy = Polly.Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => PollyHttpUtilities.ShouldRetryStatusCode(r.StatusCode, _options))
            .WaitAndRetryAsync(delays, (outcome, delay, attempt, _) =>
            {
                _logger?.LogWarning("Retry {Attempt} after {Delay} due to {Reason}", attempt, delay,
                    outcome.Exception?.Message ?? ($"HTTP {(int)outcome.Result.StatusCode}"));
            });

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

    private Task<T> ExecuteWithPerCallPolicyAsync<T>(Func<Task<T>> action, RequestOptions? options, CancellationToken ct)
    {
        if (options?.AdditionalRetryForStatus is null)
        {
            return action();
        }

        var delays = Backoff.DecorrelatedJitterBackoffV2(_options.BaseDelay, _options.RetryCount);
        var policy = Policy
            .Handle<FlurlHttpException>(ex => ex.StatusCode.HasValue && options.AdditionalRetryForStatus((HttpStatusCode)ex.StatusCode.Value))
            .WaitAndRetryAsync(delays, (ex, delay, attempt, _) =>
            {
                _logger?.LogWarning(ex, "Per-call retry {Attempt} after {Delay}", attempt, delay);
            });

        return policy.ExecuteAsync((_) => action(), ct);
    }
}


