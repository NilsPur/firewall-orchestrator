namespace FWO.Ai.Harness;

/// <summary>
/// Checks the endpoints the harness connects to. Endpoints that receive a secret (an API key or the user's JWT)
/// must use https, unless they are a loopback address, so that the secret never crosses the network in clear text.
/// </summary>
internal static class EndpointGuard
{
    /// <summary>
    /// Requires an absolute http or https URI.
    /// </summary>
    /// <param name="endpoint">Endpoint to check</param>
    /// <param name="purpose">What the endpoint is used for, for the error message</param>
    /// <exception cref="ArgumentException">The endpoint is relative or uses another scheme.</exception>
    internal static void RequireHttpOrHttps(Uri endpoint, string purpose)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        bool isHttpOrHttps = endpoint.IsAbsoluteUri && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps);
        if (!isHttpOrHttps)
        {
            throw new ArgumentException($"The {purpose} must be an absolute http or https URI.", nameof(endpoint));
        }
    }

    /// <summary>
    /// Requires an absolute https URI, or an http URI of a loopback address, for endpoints a secret is sent to.
    /// The endpoint itself is not part of the error message, as it may contain user information.
    /// </summary>
    /// <param name="endpoint">Endpoint to check</param>
    /// <param name="purpose">What the endpoint is used for, for the error message</param>
    /// <exception cref="ArgumentException">The endpoint is relative, uses another scheme or plain http to a remote host.</exception>
    internal static void RequireHttpsOrLoopback(Uri endpoint, string purpose)
    {
        RequireHttpOrHttps(endpoint, purpose);
        if (endpoint.Scheme == Uri.UriSchemeHttp && !endpoint.IsLoopback)
        {
            throw new ArgumentException($"The {purpose} must use https unless it is a loopback address, as a secret is sent to it.", nameof(endpoint));
        }
    }
}
