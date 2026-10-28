using FWO.Ai.Harness;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the checks of the endpoints the harness sends secrets (API keys, JWTs) to.
/// </summary>
[TestFixture]
internal class HarnessEndpointGuardTest
{
    private const string kPurpose = "test endpoint";
    private const string kUserInfo = "admin:s3cr3t";

    /// <summary>
    /// Verifies that https endpoints and http endpoints of loopback addresses are accepted.
    /// </summary>
    [TestCase("https://middleware.example.com:8888/mcp")]
    [TestCase("http://127.0.0.1:8890/mcp")]
    [TestCase("http://[::1]:8890/mcp")]
    [TestCase("http://localhost/mcp")]
    public void RequireHttpsOrLoopback_WithHttpsOrLoopback_Accepts(string endpoint)
    {
        Assert.DoesNotThrow(() => EndpointGuard.RequireHttpsOrLoopback(new Uri(endpoint), kPurpose));
    }

    /// <summary>
    /// Verifies that plain http to a remote host is refused, without naming the endpoint in the message.
    /// </summary>
    [Test]
    public void RequireHttpsOrLoopback_WithRemoteHttp_ThrowsWithoutEndpoint()
    {
        Uri endpoint = new($"http://{kUserInfo}@middleware.example.com/mcp");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpsOrLoopback(endpoint, kPurpose))!;

        Assert.That(exception.Message, Does.Contain(kPurpose));
        Assert.That(exception.Message, Does.Not.Contain(kUserInfo));
        Assert.That(exception.Message, Does.Not.Contain("middleware.example.com"));
    }

    /// <summary>
    /// Verifies that relative endpoints and other schemes are refused by both checks.
    /// </summary>
    [TestCase("/mcp")]
    [TestCase("ftp://middleware.example.com/mcp")]
    [TestCase("file:///etc/passwd")]
    public void Require_WithRelativeOrOtherScheme_Throws(string endpoint)
    {
        Uri uri = new(endpoint, UriKind.RelativeOrAbsolute);

        Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpsOrLoopback(uri, kPurpose));
        Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpOrHttps(uri, kPurpose));
    }

    /// <summary>
    /// Verifies that plain http to a remote host is accepted where no secret is sent.
    /// </summary>
    [Test]
    public void RequireHttpOrHttps_WithRemoteHttp_Accepts()
    {
        Assert.DoesNotThrow(() => EndpointGuard.RequireHttpOrHttps(new Uri("http://ollama.example.com:11434/"), kPurpose));
    }

    /// <summary>
    /// Verifies that a missing endpoint is refused.
    /// </summary>
    [Test]
    public void RequireHttpsOrLoopback_WithoutEndpoint_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EndpointGuard.RequireHttpsOrLoopback(null!, kPurpose));
    }
}
