using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Providers;

namespace FWO.Ai.Harness.Hosting;

/// <summary>
/// Settings of the harness server, read from the configuration section <see cref="kSectionName"/>: environment
/// variables (e.g. Harness__Provider__ApiKey for the API key, which never belongs into a file of the sources) and the
/// command line. A setting that is not given has the default of its property.
/// A class rather than a record on purpose: the provider settings hold the API key.
/// </summary>
public class HarnessSettings
{
    /// <summary>Configuration section of the settings.</summary>
    public const string kSectionName = "Harness";

    /// <summary>
    /// MCP endpoint of the MCP server on the same host, see ai_mcp_native_mcp_uri in the installer inventory.
    /// </summary>
    public const string kDefaultMcpEndpoint = "http://127.0.0.1:8890/mcp";

    /// <summary>Default number of runs a user may start within any minute.</summary>
    public const int kDefaultRunsPerMinute = 10;

    /// <summary>Default number of runs a user may run at the same time.</summary>
    public const int kDefaultMaxConcurrentRunsPerUser = 3;

    /// <summary>
    /// Default maximum request body size of the AG-UI endpoint (4 MiB). The harness is stateless, so a client sends the
    /// whole conversation with every run; the compaction keeps it within the context window of the model.
    /// </summary>
    public const long kDefaultMaxRequestBodyBytes = 4L * 1024 * 1024;

    /// <summary>Largest maximum request body size that may be configured (64 MiB).</summary>
    public const long kMaxRequestBodyBytesLimit = 64L * 1024 * 1024;

    private const string kMcpEndpointPurpose = "MCP endpoint";

    /// <summary>
    /// Endpoint of the FWO MCP server (FWO.Ai.Mcp). Must use https unless it is a loopback address, as the user's
    /// JWT is sent to it.
    /// </summary>
    public Uri McpEndpoint { get; set; } = new(kDefaultMcpEndpoint);

    /// <summary>Timeout for connecting to the MCP server; the MCP client's default if not set.</summary>
    public TimeSpan? McpConnectionTimeout { get; set; }

    /// <summary>Runs (AG-UI requests) a user may start within any minute; must be positive.</summary>
    public int RunsPerMinute { get; set; } = kDefaultRunsPerMinute;

    /// <summary>Runs a user may run at the same time; must be positive.</summary>
    public int MaxConcurrentRunsPerUser { get; set; } = kDefaultMaxConcurrentRunsPerUser;

    /// <summary>
    /// Maximum request body size of the AG-UI endpoint in bytes; larger requests get 413. It bounds the conversation a
    /// client sends with each run. Must be positive and at most <see cref="kMaxRequestBodyBytesLimit"/>.
    /// </summary>
    public long MaxRequestBodyBytes { get; set; } = kDefaultMaxRequestBodyBytes;

    /// <summary>AI provider, model, credentials and context window; checked when the agent factory is created.</summary>
    public AiProviderSettings Provider { get; set; } = new();

    /// <summary>Agent loop and compaction; checked when the agent factory is created.</summary>
    public AgentSettings Agent { get; set; } = new();

    /// <summary>
    /// Reads and checks the settings of the harness server.
    /// </summary>
    /// <param name="configuration">Configuration of the harness server</param>
    /// <exception cref="ArgumentException">A setting of the server is invalid.</exception>
    public static HarnessSettings FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        HarnessSettings settings = configuration.GetSection(kSectionName).Get<HarnessSettings>() ?? new HarnessSettings();
        settings.Validate();
        return settings;
    }

    /// <summary>
    /// Checks the settings of the server; the provider and agent settings are checked by the agent factory.
    /// </summary>
    /// <exception cref="ArgumentException">A setting of the server is invalid.</exception>
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(McpEndpoint);
        EndpointGuard.RequireHttpsOrLoopback(McpEndpoint, kMcpEndpointPurpose);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RunsPerMinute);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConcurrentRunsPerUser);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRequestBodyBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxRequestBodyBytes, kMaxRequestBodyBytesLimit);
        if (McpConnectionTimeout is TimeSpan connectionTimeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(connectionTimeout, TimeSpan.Zero);
        }
    }
}
