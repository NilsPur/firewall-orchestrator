using FWO.Ai.Mcp;
using FWO.Config.File;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// JWT auth
builder.Services.AddFwoJwtAuthentication(ConfigFile.JwtPublicKey);

// MCP services: the transport to use (http), the tools and prompts to register and the rate limits per user.
builder.Services.AddFwoMcpServer(McpRateLimitSettings.FromConfigFile());

WebApplication app = builder.Build();
// no https redirection: Kestrel listens on loopback http only, behind the Apache reverse proxy that terminates TLS
app.MapFwoMcp();

app.Run();
