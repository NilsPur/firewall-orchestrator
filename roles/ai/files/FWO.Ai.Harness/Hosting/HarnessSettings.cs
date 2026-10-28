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

    /// <summary>Default number of conversations (AG-UI threads) kept per user.</summary>
    public const int kDefaultMaxSessionsPerUser = 10;

    /// <summary>Default number of runs a user may run at the same time, each on a thread of its own.</summary>
    public const int kDefaultMaxConcurrentRunsPerUser = 3;

    /// <summary>Default time after which an unused conversation is dropped.</summary>
    public static readonly TimeSpan kDefaultSessionIdleTimeout = TimeSpan.FromHours(4);

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

    /// <summary>
    /// Conversations (AG-UI threads) kept per user; when a user starts one more, the least recently used one is
    /// dropped. Must be positive.
    /// </summary>
    public int MaxSessionsPerUser { get; set; } = kDefaultMaxSessionsPerUser;

    /// <summary>
    /// Runs a user may run at the same time, each on a thread of its own: a thread runs one run at a time. Must be
    /// positive and at most <see cref="MaxSessionsPerUser"/>, as more runs would drop each other's conversations.
    /// </summary>
    public int MaxConcurrentRunsPerUser { get; set; } = kDefaultMaxConcurrentRunsPerUser;

    /// <summary>Time after which a conversation that was not used is dropped; must be positive.</summary>
    public TimeSpan SessionIdleTimeout { get; set; } = kDefaultSessionIdleTimeout;

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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxSessionsPerUser);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConcurrentRunsPerUser);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxConcurrentRunsPerUser, MaxSessionsPerUser);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(SessionIdleTimeout, TimeSpan.Zero);
        if (McpConnectionTimeout is TimeSpan connectionTimeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(connectionTimeout, TimeSpan.Zero);
        }
    }
}
