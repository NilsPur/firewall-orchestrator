using FWO.Ai.Mcp.Prompts;
using FWO.Ai.Mcp.Tools;
using FWO.Basics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Net;

namespace FWO.Ai.Mcp;

/// <summary>
/// Service and pipeline setup of the MCP server, used by Program and by the unit tests.
/// </summary>
internal static class McpServerSetup
{
    /// <summary>
    /// Path of the MCP endpoint. The middleware's Apache vhost publishes it under the same path,
    /// see ai_mcp_path in the installer inventory, which has to stay in step.
    /// </summary>
    public const string kMcpPath = "/mcp";

    private const string kNameClaimType = "unique_name";
    private const string kRoleClaimType = "role";

    /// <summary>
    /// Accepts the JWTs issued by the middleware server, validated with the given public key.
    /// By default only users holding an FWO user role are authorized, see <see cref="CreateFwoUserPolicy"/>.
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
            options.TokenValidationParameters = new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateAudience = true,
                ValidAudiences = [JwtConstants.Audience],
                ValidateIssuer = true,
                ValidIssuers = [JwtConstants.Issuer],
                ValidateLifetime = true,
                NameClaimType = kNameClaimType,
                RoleClaimType = kRoleClaimType,
                IssuerSigningKey = jwtPublicKey
            };
        });
        // the default policy applies to the MCP endpoint and to every plain [Authorize] on a tool
        services.AddAuthorization(options => options.DefaultPolicy = CreateFwoUserPolicy());
        return services;
    }

    /// <summary>
    /// Requires a user holding at least one role that is neither technical nor anonymous. The middleware server
    /// also signs anonymous bootstrap tokens (issued without credentials) and tokens of technical users,
    /// which must not give access to the MCP server.
    /// </summary>
    private static AuthorizationPolicy CreateFwoUserPolicy()
    {
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.FindAll(kRoleClaimType).Any(role => !RoleGroups.IsTechnicalOrAnonymous(role.Value)))
            .Build();
    }

    /// <summary>
    /// Adds the MCP services: the transport to use (http), the tools and prompts to register and the rate limiting.
    /// </summary>
    public static IServiceCollection AddFwoMcpServer(this IServiceCollection services, McpRateLimitSettings rateLimitSettings)
    {
        services.AddFwoMcpRateLimiting(rateLimitSettings);
        services.AddHttpContextAccessor();
        services
            .AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .AddAuthorizationFilters()
            .WithTools<RandomNumberTools>()
            .WithTools<UserTools>()
            .WithPrompts<RequestApplicationPrompts>();
        return services;
    }

    /// <summary>
    /// Maps the MCP endpoint at <see cref="kMcpPath"/>, reachable only with a valid JWT of a user holding an FWO user role,
    /// rate limited per user and with a limited request body size. Requests arrive through the local Apache reverse proxy,
    /// see <see cref="CreateReverseProxyForwardingOptions"/>.
    /// </summary>
    public static WebApplication MapFwoMcp(this WebApplication app)
    {
        app.UseForwardedHeaders(CreateReverseProxyForwardingOptions());
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapMcp(kMcpPath)
            .RequireAuthorization()
            .RequireRateLimiting(McpRateLimiting.kMcpPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(McpRateLimiting.kMaxRequestBodyBytes));
        return app;
    }

    /// <summary>
    /// Creates forwarding settings that trust the HTTPS scheme and the client address from the local Apache reverse proxy
    /// only. Only the last X-Forwarded-For entry, the one Apache appended, is used
    /// (ForwardLimit 1), so a client cannot choose its own address by sending the header itself.
    /// </summary>
    internal static ForwardedHeadersOptions CreateReverseProxyForwardingOptions()
    {
        ForwardedHeadersOptions options = new()
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
            ForwardLimit = 1
        };
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
        return options;
    }
}
