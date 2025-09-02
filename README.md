## Resilient HTTP Client (Flurl + Polly)

A small, reusable .NET 8 library that wraps Flurl with Polly-based retry and timeout policies. Designed for SOLID and high cohesion: interface-driven, configurable options, and DI-friendly.

### Install

Add packages (already referenced in the project if you use this repo):

```bash
dotnet add package Flurl.Http --version 4.0.0
dotnet add package Polly --version 7.2.3
```

If you need the `PolicyHttpMessageHandler` variant, you can also add:

```bash
dotnet add package Microsoft.Extensions.Http.Polly --version 8.0.8
```

### Library Surface

- `IResilientHttpClient`
  - `Task<T> GetAsync<T>(string url, CancellationToken ct = default)`
  - `Task<string> GetStringAsync(string url, CancellationToken ct = default)`
  - `Task<TResponse> PostJsonAsync<TRequest,TResponse>(string url, TRequest body, CancellationToken ct = default)`
  - `Task<TResponse> PutJsonAsync<TRequest,TResponse>(string url, TRequest body, CancellationToken ct = default)`
  - `Task DeleteAsync(string url, CancellationToken ct = default)`

- `ResilienceOptions`
  - `int RetryCount` (default: 3)
  - `TimeSpan BaseDelay` (default: 200ms, exponential backoff)
  - `TimeSpan? Timeout` (default: 30s)
  - `Func<HttpStatusCode, bool>? AdditionalHttpRetry` (extend retryable status codes)

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

    public Task<Foo> GetFooAsync(string id, CancellationToken ct)
        => _http.GetAsync<Foo>($"https://api.example.com/foo/{id}", ct);

    public Task<string> GetHealthAsync(CancellationToken ct)
        => _http.GetStringAsync("https://api.example.com/health", ct);

    public Task<Foo> CreateFooAsync(FooCreate req, CancellationToken ct)
        => _http.PostJsonAsync<FooCreate, Foo>("https://api.example.com/foo", req, ct);
}
```

### Retry Behavior

The default policy retries on network errors and transient HTTP status codes: 408, 429, and 5xx. Backoff uses exponential growth based on `BaseDelay`. You can extend with `AdditionalHttpRetry` to include more status codes.

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


