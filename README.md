## Resilient HTTP Client (Flurl + Polly)

A small, reusable .NET 8 library that wraps Flurl with Polly-based retry and timeout policies. Designed for SOLID and high cohesion: interface-driven, configurable options, and DI-friendly.

### Install

Add packages (already referenced in the project if you use this repo):

```bash
dotnet add package Flurl.Http --version 4.0.0
dotnet add package Polly --version 7.2.4
dotnet add package Polly.Contrib.WaitAndRetry --version 1.1.1
```

Optional:
- `Microsoft.Extensions.Http.Polly` if you want to use built-in handlers.

### Library Surface

- `IResilientHttpClient`
  - `Task<T> GetAsync<T>(string url, RequestOptions? options = null, CancellationToken ct = default)`
  - `Task<string> GetStringAsync(string url, RequestOptions? options = null, CancellationToken ct = default)`
  - `Task<TResponse> PostJsonAsync<TRequest,TResponse>(string url, TRequest body, RequestOptions? options = null, CancellationToken ct = default)`
  - `Task<TResponse> PutJsonAsync<TRequest,TResponse>(string url, TRequest body, RequestOptions? options = null, CancellationToken ct = default)`
  - `Task DeleteAsync(string url, RequestOptions? options = null, CancellationToken ct = default)`

- `ResilienceOptions`
  - `int RetryCount` (default: 3)
  - `TimeSpan BaseDelay` (default: 200ms, jittered exponential backoff)
  - `TimeSpan? Timeout` (default: 30s)
  - `Func<HttpStatusCode, bool>? AdditionalHttpRetry` (extend retryable status codes)

- `RequestOptions`
  - `string? BearerToken`
  - `(string UserName, string Password)? BasicAuth`
  - `IReadOnlyCollection<KeyValuePair<string,string>>? Headers`
  - `Func<HttpStatusCode, bool>? AdditionalRetryForStatus`

- DI extension: `ServiceCollectionExtensions.AddResilientHttpClient(...)`

### Register with DI

```csharp
services.AddResilientHttpClient(o =>
{
    o.RetryCount = 3;
    o.BaseDelay = TimeSpan.FromMilliseconds(200);
    o.Timeout = TimeSpan.FromSeconds(30);
    o.AdditionalHttpRetry = status => status == (HttpStatusCode)418; // optional
});
```

### Usage

```csharp
public class MyService
{
    private readonly IResilientHttpClient _http;

    public MyService(IResilientHttpClient http) => _http = http;

    public Task<Foo> GetFooAsync(string id, string token, string tenantId, CancellationToken ct)
        => _http.GetAsync<Foo>($"https://api.example.com/foo/{id}", new RequestOptions
        {
            BearerToken = token,
            Headers = new[] { new KeyValuePair<string,string>("X-Tenant", tenantId) }
        }, ct);

    public Task<string> GetHealthAsync(CancellationToken ct)
        => _http.GetStringAsync("https://api.example.com/health", null, ct);

    public Task<Foo> CreateFooAsync(FooCreate req, string user, string pass, CancellationToken ct)
        => _http.PostJsonAsync<FooCreate, Foo>("https://api.example.com/foo", req, new RequestOptions
        {
            BasicAuth = (user, pass)
        }, ct);
}
```

### Retry Behavior

The default policy retries on network errors and transient HTTP status codes: 408, 429, and 5xx. Backoff uses jittered exponential delays via `Polly.Contrib.WaitAndRetry` based on `BaseDelay`.

- Global extension: configure `ResilienceOptions.AdditionalHttpRetry` to add more status codes globally.
- Per-call extension: pass `RequestOptions.AdditionalRetryForStatus` to add retry logic for a specific call.

Example (per-call retry on 409 Conflict):

```csharp
var result = await _http.PostJsonAsync<Req, Res>(
    "https://api.example.com/items",
    requestBody,
    new RequestOptions {
        AdditionalRetryForStatus = status => status == HttpStatusCode.Conflict
    },
    ct);
```

### Without DI (Factory)

```csharp
var client = ResilientHttpClientFactory.Create(new ResilienceOptions
{
    RetryCount = 4,
    BaseDelay = TimeSpan.FromMilliseconds(150),
    Timeout = TimeSpan.FromSeconds(20)
});

var data = await client.GetAsync<MyDto>("https://api.example.com/data");
```

### Notes

- Built on Flurl.Http 4.x API surface.
- Thread-safe `FlurlClient` with configured `HttpClient` and Polly delegating handler.
- High cohesion: policy config is centralized in `ResilienceOptions`; consumers depend only on `IResilientHttpClient`.

### License

This project is licensed under the MIT License - see `LICENSE` for details.


