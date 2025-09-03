using System;
using System.Collections.Generic;
using System.Net;

namespace ResilientHttp.Configuration;

public sealed class RequestOptions
{
    public string? BearerToken { get; init; }
    public (string UserName, string Password)? BasicAuth { get; init; }
    public IReadOnlyCollection<KeyValuePair<string, string>>? Headers { get; init; }
    public Func<HttpStatusCode, bool>? AdditionalRetryForStatus { get; init; }
}


