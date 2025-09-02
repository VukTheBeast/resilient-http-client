namespace resilient_http_client.ResilientHttp.Abstractions;

public interface IResilientHttpClient
{
    Task<T> GetAsync<T>(string url, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task<string> GetStringAsync(string url, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task<TResponse> PostJsonAsync<TRequest, TResponse>(string url, TRequest body, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task<TResponse> PutJsonAsync<TRequest, TResponse>(string url, TRequest body, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string url, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);
}


