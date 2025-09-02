namespace resilient_http_client.ResilientHttp.Abstractions;

public interface IResilientHttpClient
{
    Task<T> GetAsync<T>(string url, CancellationToken cancellationToken = default);
    Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);
    Task<TResponse> PostJsonAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default);
    Task<TResponse> PutJsonAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default);
    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
}


