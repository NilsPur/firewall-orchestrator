using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Mcp;
using FWO.Basics;
using FWO.Config.File;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;

namespace FWO.Ai.Harness.Hosting;

/// <summary>
/// Service and pipeline setup of the harness server, used by Program and by the unit tests. The server offers the
/// agent of the FWO users over AG-UI (Agent-User Interaction Protocol): a client posts a run and receives the answer
/// as server-sent events.
/// </summary>
internal static class HarnessServerSetup
{
    /// <summary>Path of the AG-UI endpoint.</summary>
    public const string kAgUiPath = "/agui";

    private const string kBearerScheme = "Bearer";

    /// <summary>
    /// Accepts the JWTs issued by the middleware server, validated with the given public key. By default only users
    /// holding an FWO user role and a directory identity are authorized, see <see cref="CreateFwoUserPolicy"/>.
    /// </summary>
    public static IServiceCollection AddFwoJwtAuthentication(this IServiceCollection services, SecurityKey jwtPublicKey)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = JwtValidation.CreateParameters(jwtPublicKey);
        });
        services.AddAuthorization(options => options.DefaultPolicy = CreateFwoUserPolicy());
        return services;
    }

    /// <summary>
    /// Adds the services of the AG-UI server: the agent factory and the rate limiting. The server is stateless: no
    /// session store is registered, so the AG-UI hosting runs each request on a session of its own, and the client sends
    /// the whole conversation with every run (see <see cref="TurnInput"/>).
    /// </summary>
    /// <param name="services">Services of the harness server</param>
    /// <param name="settings">Settings of the harness server</param>
    /// <param name="createAgentFactory">Creates the agent factory, which the service provider disposes at shutdown. It is
    /// called by the first run, and again by the next run as long as it throws, e.g. while no AI provider is configured.</param>
    public static IServiceCollection AddFwoAgentServer(this IServiceCollection services, HarnessSettings settings,
        Func<IServiceProvider, FwoAgentFactory> createAgentFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(createAgentFactory);
        services.AddSingleton(settings);
        services.AddSingleton(createAgentFactory);
        services.AddAGUIServer();
        services.AddFwoHarnessRateLimiting(settings.RunsPerMinute, settings.MaxConcurrentRunsPerUser);
        return services;
    }

    /// <summary>
    /// Maps the AG-UI endpoint at <see cref="kAgUiPath"/>, reachable only with a valid JWT of a user holding an FWO user
    /// role, rate limited per user, with a limited number of runs per user at a time and a limited request body size
    /// (<see cref="HarnessSettings.MaxRequestBodyBytes"/>). Each request runs the agent of the calling user, which calls
    /// the MCP server with the JWT of the request. The agent factory is only created by the first run, so the server starts without a configured
    /// AI provider; until one is configured, every run fails with the exception of the factory.
    /// </summary>
    public static WebApplication MapFwoAgent(this WebApplication app)
    {
        HarnessSettings settings = app.Services.GetRequiredService<HarnessSettings>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapAGUIServer(kAgUiPath, context => SelectUserAgent(context, context.RequestServices.GetRequiredService<FwoAgentFactory>(), settings))
            .RequireAuthorization()
            .RequireRateLimiting(HarnessRateLimiting.kRunPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(settings.MaxRequestBodyBytes));
        return app;
    }

    /// <summary>
    /// Creates the agent of the calling user, calling the MCP server with the bearer token of the request, which the
    /// authorization has validated. Returns null, i.e. 404, for a request without bearer token.
    /// </summary>
    internal static AIAgent? SelectUserAgent(HttpContext context, FwoAgentFactory agentFactory, HarnessSettings settings)
    {
        string? accessToken = GetBearerToken(context.Request);
        if (accessToken == null)
        {
            return null;
        }
        McpConnectionSettings userConnection = new()
        {
            Endpoint = settings.McpEndpoint,
            AccessTokenProvider = _ => ValueTask.FromResult(accessToken),
            ConnectionTimeout = settings.McpConnectionTimeout
        };
        return agentFactory.CreateUserAgent(userConnection);
    }

    /// <summary>
    /// Returns the bearer token of the request's authorization header, or null if there is none.
    /// </summary>
    private static string? GetBearerToken(HttpRequest request)
    {
        bool hasAuthorization = AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out AuthenticationHeaderValue? authorization);
        bool isBearer = hasAuthorization && string.Equals(authorization?.Scheme, kBearerScheme, StringComparison.OrdinalIgnoreCase);
        return isBearer && !string.IsNullOrWhiteSpace(authorization?.Parameter) ? authorization.Parameter : null;
    }

    /// <summary>
    /// Requires a user holding at least one FWO user role, and a directory identity
    /// (<see cref="JwtClaimTypes.kUserDn"/>), which partitions the rate limits.
    /// </summary>
    private static AuthorizationPolicy CreateFwoUserPolicy()
    {
        return FwoUserPolicy.CreateBuilder()
            .RequireAssertion(context => !string.IsNullOrWhiteSpace(context.User.FindFirst(JwtClaimTypes.kUserDn)?.Value))
            .Build();
    }
}
