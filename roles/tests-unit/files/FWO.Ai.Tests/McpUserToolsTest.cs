using FWO.Ai.Mcp.Tools;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the MCP user tools outside of an authenticated request; the authenticated case is covered by
/// <see cref="McpServerAuthenticationTest"/>.
/// </summary>
[TestFixture]
internal class McpUserToolsTest
{
    /// <summary>
    /// Verifies that an empty user is returned when there is no current request.
    /// </summary>
    [Test]
    public void GetCurrentUser_WithoutHttpContext_ReturnsEmptyUser()
    {
        CurrentUser currentUser = new UserTools(new HttpContextAccessor()).GetCurrentUser();

        Assert.That(currentUser.Name, Is.Empty);
        Assert.That(currentUser.Roles, Is.Empty);
    }

    /// <summary>
    /// Verifies that an empty user is returned for a request without authenticated user.
    /// </summary>
    [Test]
    public void GetCurrentUser_WithAnonymousUser_ReturnsEmptyUser()
    {
        HttpContextAccessor httpContextAccessor = new() { HttpContext = new DefaultHttpContext() };

        CurrentUser currentUser = new UserTools(httpContextAccessor).GetCurrentUser();

        Assert.That(currentUser.Name, Is.Empty);
        Assert.That(currentUser.Roles, Is.Empty);
    }
}
