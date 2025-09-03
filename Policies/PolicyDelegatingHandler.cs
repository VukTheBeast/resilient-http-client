using Polly;

namespace ResilientHttp.Policies;

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


