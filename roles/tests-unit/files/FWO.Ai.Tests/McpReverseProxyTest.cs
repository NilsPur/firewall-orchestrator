using FWO.Ai.Mcp;
using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Net;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests how the MCP server sits behind the middleware's Apache reverse proxy: the endpoint path Apache publishes
/// and the forwarded headers it trusts.
/// </summary>
[TestFixture]
internal class McpReverseProxyTest
{
    private const string kRootPath = "/";
    private const string kForwardedForHeader = "X-Forwarded-For";
    private const string kForwardedProtoHeader = "X-Forwarded-Proto";
    private const string kHttpsScheme = "https";
    private const string kHttpScheme = "http";
    private const string kClientAddress = "192.0.2.30";
    private const string kSpoofedAndAppendedAddresses = "127.0.0.1, " + kClientAddress;
    private const string kNonLoopbackPeerAddress = "192.0.2.40";

    /// <summary>
    /// Verifies that the MCP endpoint is served at the path the Apache vhost publishes (positive control for the test below).
    /// </summary>
    [Test]
    public async Task McpPath_ServesTheMcpEndpoint()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();

        using HttpResponseMessage response = await McpTestServer.PostTo(httpClient, McpServerSetup.kMcpPath,
            McpTestServer.CreateMiddlewareToken(Roles.Reporter), McpTestServer.kToolsListRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Verifies that the root path is not the MCP endpoint, so only the path Apache publishes reaches it.
    /// </summary>
    [Test]
    public async Task RootPath_IsNotTheMcpEndpoint()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();

        using HttpResponseMessage response = await McpTestServer.PostTo(httpClient, kRootPath,
            McpTestServer.CreateMiddlewareToken(Roles.Reporter), McpTestServer.kToolsListRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// Verifies that the HTTPS scheme and the client address are forwarded from loopback proxies only.
    /// </summary>
    [Test]
    public void CreateReverseProxyForwardingOptions_ForwardsProtoAndClientFromLoopbackProxies()
    {
        ForwardedHeadersOptions options = McpServerSetup.CreateReverseProxyForwardingOptions();

        Assert.That(options.ForwardedHeaders, Is.EqualTo(ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor));
        Assert.That(options.ForwardLimit, Is.EqualTo(1));
        Assert.That(options.KnownProxies, Does.Contain(IPAddress.Loopback));
        Assert.That(options.KnownProxies, Does.Contain(IPAddress.IPv6Loopback));
    }

    /// <summary>
    /// Verifies that a request from the local proxy gets the scheme and the client address Apache appended,
    /// not an address the client sent itself.
    /// </summary>
    [Test]
    public async Task ForwardedHeaders_FromLoopbackProxy_UseTheValuesAppendedByTheProxy()
    {
        DefaultHttpContext context = CreateProxiedContext(IPAddress.Loopback);

        await ApplyForwardedHeaders(context);

        Assert.That(context.Request.Scheme, Is.EqualTo(kHttpsScheme));
        Assert.That(context.Connection.RemoteIpAddress, Is.EqualTo(IPAddress.Parse(kClientAddress)));
    }

    /// <summary>
    /// Verifies that forwarded headers sent by anything other than the local proxy are ignored.
    /// </summary>
    [Test]
    public async Task ForwardedHeaders_FromNonLoopbackPeer_AreIgnored()
    {
        IPAddress peerAddress = IPAddress.Parse(kNonLoopbackPeerAddress);
        DefaultHttpContext context = CreateProxiedContext(peerAddress);

        await ApplyForwardedHeaders(context);

        Assert.That(context.Request.Scheme, Is.EqualTo(kHttpScheme));
        Assert.That(context.Connection.RemoteIpAddress, Is.EqualTo(peerAddress));
    }

    private static DefaultHttpContext CreateProxiedContext(IPAddress peerAddress)
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = kHttpScheme;
        context.Connection.RemoteIpAddress = peerAddress;
        context.Request.Headers[kForwardedForHeader] = kSpoofedAndAppendedAddresses;
        context.Request.Headers[kForwardedProtoHeader] = kHttpsScheme;
        return context;
    }

    private static async Task ApplyForwardedHeaders(HttpContext context)
    {
        ForwardedHeadersMiddleware middleware = new(_ => Task.CompletedTask, NullLoggerFactory.Instance,
            Options.Create(McpServerSetup.CreateReverseProxyForwardingOptions()));
        await middleware.Invoke(context);
    }
}
