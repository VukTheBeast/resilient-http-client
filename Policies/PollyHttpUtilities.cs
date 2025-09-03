using System.Net;
using ResilientHttp.Configuration;

namespace ResilientHttp.Policies;

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


