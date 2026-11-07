namespace FWO.Ai.Client.AgUi;

/// <summary>
/// Builds and checks the AG-UI endpoint of the harness. The user's JWT is sent to it, so it must use https, unless it
/// is a loopback address, so that the token never crosses the network in clear text (like the EndpointGuard of the
/// harness).
/// </summary>
internal static class AgUiEndpoint
{
    private const string kPathSeparator = "/";

    /// <summary>
    /// Returns the AG-UI endpoint below the middleware server uri.
    /// </summary>
    /// <param name="middlewareServerUri">Uri of the middleware server, e.g. https://host:8888/</param>
    /// <exception cref="ArgumentNullException">The uri is null.</exception>
    /// <exception cref="ArgumentException">The uri is relative, uses another scheme or plain http to a remote host.</exception>
    internal static Uri Create(Uri middlewareServerUri)
    {
        RequireHttpsOrLoopback(middlewareServerUri);
        return new Uri(WithTrailingSlash(middlewareServerUri), FwoAgentChatClientFactory.kAgUiPath);
    }

    /// <summary>
    /// Requires an absolute https uri, or an http uri of a loopback address. The uri itself is not part of the error
    /// message, as it may contain user information.
    /// </summary>
    /// <exception cref="ArgumentNullException">The uri is null.</exception>
    /// <exception cref="ArgumentException">The uri is relative, uses another scheme or plain http to a remote host.</exception>
    internal static void RequireHttpsOrLoopback(Uri middlewareServerUri)
    {
        ArgumentNullException.ThrowIfNull(middlewareServerUri);
        bool isHttpOrHttps = middlewareServerUri.IsAbsoluteUri
            && (middlewareServerUri.Scheme == Uri.UriSchemeHttp || middlewareServerUri.Scheme == Uri.UriSchemeHttps);
        if (!isHttpOrHttps)
        {
            throw new ArgumentException("The middleware server uri must be an absolute http or https uri.", nameof(middlewareServerUri));
        }
        if (middlewareServerUri.Scheme == Uri.UriSchemeHttp && !middlewareServerUri.IsLoopback)
        {
            throw new ArgumentException("The middleware server uri must use https unless it is a loopback address, as the access token is sent to it.",
                nameof(middlewareServerUri));
        }
    }

    /// <summary>
    /// Returns the uri with a path ending in a slash, so that the AG-UI path is added below it instead of replacing its
    /// last segment.
    /// </summary>
    private static Uri WithTrailingSlash(Uri uri)
    {
        if (uri.AbsolutePath.EndsWith(kPathSeparator, StringComparison.Ordinal))
        {
            return uri;
        }
        UriBuilder builder = new(uri);
        builder.Path += kPathSeparator;
        return builder.Uri;
    }
}
