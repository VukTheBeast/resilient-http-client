namespace resilient_http_client.ResilientHttp.Abstractions;

public interface IResilientHttpClient
{
    Task<T> GetAsync<T>(string url, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task<string> GetStringAsync(string url, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task<TResponse> PostJsonAsync<TRequest, TResponse>(string url, TRequest body, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task<TResponse> PutJsonAsync<TRequest, TResponse>(string url, TRequest body, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string url, ResilientHttp.Configuration.RequestOptions? options = null, CancellationToken cancellationToken = default);

    Task<TResponse> PostStreamAsync<TResponse>(string url, Stream data, string formFieldName, string fileName, ResilientHttp.Configuration.RequestOptions? options = null, string? contentType = null, CancellationToken cancellationToken = default);

    Task<TResponse> PostStreamAsync<TResponse>(string url, Func<Stream> streamFactory, string formFieldName, string fileName, ResilientHttp.Configuration.RequestOptions? options = null, string? contentType = null, CancellationToken cancellationToken = default);
}


