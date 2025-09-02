using Flurl.Http;
using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using resilient_http_client.ResilientHttp.Abstractions;
using resilient_http_client.ResilientHttp.Configuration;
using resilient_http_client.ResilientHttp.Policies;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

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

    public async Task<TResponse> PostStreamAsync<TResponse>(string url, Stream data, string formFieldName, string fileName, RequestOptions? options = null, string? contentType = null, CancellationToken cancellationToken = default)
        => await ExecuteWithPerCallPolicyAsync(
            async () =>
            {
                using var streamContent = new StreamContent(data);
                if (!string.IsNullOrEmpty(contentType))
                {
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }
                using var multipart = new MultipartFormDataContent();
                multipart.Add(streamContent, formFieldName, fileName);

                var resp = await CreateRequest(url, options).SendAsync(HttpMethod.Post, multipart, cancellationToken: cancellationToken);
                return await resp.GetJsonAsync<TResponse>();
            },
            options,
            cancellationToken);

    public async Task<TResponse> PostStreamAsync<TResponse>(string url, Func<Stream> streamFactory, string formFieldName, string fileName, RequestOptions? options = null, string? contentType = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("[POST_STREAM] Starting execution for URL: {Url}", url);
        return await ExecuteWithPerCallPolicyAsync(
            async () =>
            {
                _logger?.LogInformation("[STREAM] Invoking stream factory...");
                await using var stream = streamFactory();
                if (stream == null)
                {
                    _logger?.LogError("[STREAM] Stream factory returned null.");
                    throw new InvalidOperationException("Stream factory returned a null stream.");
                }
                _logger?.LogInformation("[STREAM] Created stream. Length: {Length}, CanRead: {CanRead}", stream.Length, stream.CanRead);

                using var streamContent = new StreamContent(stream);
                if (!string.IsNullOrEmpty(contentType))
                {
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }
                using var multipart = new MultipartFormDataContent();
                multipart.Add(streamContent, formFieldName, fileName);

                var resp = await CreateRequest(url, options).SendAsync(HttpMethod.Post, multipart, cancellationToken: cancellationToken);
                var json = await resp.GetJsonAsync<TResponse>();
                _logger?.LogInformation("[POST_STREAM] Successfully completed request.");
                return json;
            },
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


