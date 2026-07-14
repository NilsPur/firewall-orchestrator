using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Ai;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiAssistantSettingsTest
    {
        [Test]
        public async Task ExecutionModeAuthorizeView_EmptyRoles_AllowsAuthenticatedUser()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ExecutionModeAuthorizeView>(child => child
                    .Add(view => view.Roles, "")
                    .AddChildContent("<span id=\"authorized\">authorized</span>")));

            Assert.That(component.Markup, Does.Contain("authorized"));
        }

        [Test]
        public void SettingsAssistant_ReasoningEfforts_MatchEnumValues()
        {
            PropertyInfo property = typeof(SettingsAssistant).GetProperty("ReasoningEfforts", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMemberException(typeof(SettingsAssistant).FullName, "ReasoningEfforts");

            IReadOnlyList<ReasoningEffort> efforts = (IReadOnlyList<ReasoningEffort>)property.GetValue(null)!;

            Assert.That(efforts, Is.EqualTo(Enum.GetValues<ReasoningEffort>()));
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = [Roles.Reporter] } });
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<AuthenticationStateProvider>(new AuthenticatedUserStateProvider());
            return context;
        }

        private sealed class AuthenticatedUserStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "assistant-user")
            ], "Test"));

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }
    }
}
