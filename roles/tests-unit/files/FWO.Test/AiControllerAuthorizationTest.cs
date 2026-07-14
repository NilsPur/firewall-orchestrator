using FWO.Basics;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class AiControllerAuthorizationTest
{
    [Test]
    public void AiProviderTestEndpoint_RemainsAdminOnly()
    {
        AuthorizeAttribute authorize = GetAuthorizeAttribute(nameof(AiController.TestProvider));

        Assert.That(authorize.Roles, Is.EqualTo(Roles.Admin));
    }

    [Test]
    public void AiSettingsEndpoint_RemainsAdminAndAuditorOnly()
    {
        AuthorizeAttribute authorize = GetAuthorizeAttribute(nameof(AiController.GetSettings));

        Assert.That(authorize.Roles, Is.EqualTo($"{Roles.Admin}, {Roles.Auditor}"));
    }

    [Test]
    public void AssistantSettingsEndpoint_AllowsAnyAuthenticatedUser()
    {
        MethodInfo method = typeof(AiController).GetMethod(nameof(AiController.GetAssistantSettings))!;
        AuthorizeAttribute? methodAuthorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.That(methodAuthorize, Is.Null);
        Assert.That(method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>()?.Template,
            Is.EqualTo("Assistant/Settings"));
    }

    private static AuthorizeAttribute GetAuthorizeAttribute(string methodName)
    {
        MethodInfo method = typeof(AiController).GetMethod(methodName)!;
        AuthorizeAttribute? authorizeAttribute = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.That(authorizeAttribute, Is.Not.Null, $"Expected [Authorize] on {nameof(AiController)}.{methodName}.");
        return authorizeAttribute!;
    }
}
