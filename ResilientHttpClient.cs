using System.Net;
using System.Net.Http;
using Flurl.Http;
using Flurl.Http.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace resilient_http_client;

public interface IResilientHttpClient
{
    Task<T> GetAsync<T>(string url, CancellationToken cancellationToken = default);
    Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);
    Task<TResponse> PostJsonAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default);
    Task<TResponse> PutJsonAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default);
    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class ResilienceOptions
{
    public int RetryCount { get; init; } = 3;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan? Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public Func<HttpStatusCode, bool>? AdditionalHttpRetry { get; init; }
}

public static class ResilientHttpClientFactory
{
    public static IResilientHttpClient Create(ResilienceOptions? options = null)
        => new FlurlPollyResilientHttpClient(options ?? new ResilienceOptions());
}

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
        var retryPolicy = Policy<HttpResponseMessage>
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

internal static class PollyHttpUtilities
{
    public static bool ShouldRetryStatusCode(HttpStatusCode statusCode, ResilienceOptions options)
    {
        bool isTransient = statusCode == HttpStatusCode.RequestTimeout
                            || (int)statusCode == 429
                            || (int)statusCode >= 500;

        if (options.AdditionalHttpRetry is null)
        {
            return isTransient;
        }

        return isTransient || options.AdditionalHttpRetry(statusCode);
    }
}

internal sealed class PolicyDelegatingHandler : DelegatingHandler
{
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;

    public PolicyDelegatingHandler(IAsyncPolicy<HttpResponseMessage> policy)
    {
        _policy = policy;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _policy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken);
    }
}

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
