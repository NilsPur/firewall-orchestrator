using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using FWO.Config.File;

const string kAspNetCoreLogCategory = "Microsoft.AspNetCore";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
HarnessSettings settings = HarnessSettings.FromConfiguration(builder.Configuration);

builder.Logging.AddFilter(kAspNetCoreLogCategory, LogLevel.Warning);

// JWT auth: the JWTs of the middleware server, as accepted by the MCP server
builder.Services.AddFwoJwtAuthentication(ConfigFile.JwtPublicKey);

// AG-UI services: the agent of the FWO users with the configured AI provider, the sessions per user and the rate limits.
builder.Services.AddFwoAgentServer(settings,
    serviceProvider => new FwoAgentFactory(settings.Provider, settings.Agent, serviceProvider.GetService<ILoggerFactory>()));

WebApplication app = builder.Build();
app.MapFwoAgent();

app.Run();
