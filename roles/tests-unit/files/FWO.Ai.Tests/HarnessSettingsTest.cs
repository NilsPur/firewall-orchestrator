using FWO.Ai.Harness.Hosting;
using FWO.Ai.Harness.Providers;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests reading the settings of the harness server from the configuration: the defaults, the configured values,
/// and the checks of the server settings.
/// </summary>
[TestFixture]
internal class HarnessSettingsTest
{
    private const string kModel = "test-model";
    private const string kApiKey = "sk-harness-test-key-4711";
    private const string kLoopbackMcpEndpoint = "http://127.0.0.1:9999/mcp";
    private const string kRemoteHttpMcpEndpoint = "http://middleware.example.com/mcp";
    private const string kConnectionTimeout = "00:00:30";
    private const string kIdleTimeout = "01:00:00";
    private const int kRunsPerMinute = 5;
    private const int kMaxSessionsPerUser = 3;
    private const int kMaxConcurrentRunsPerUser = 2;
    private const int kContextWindowTokens = 200000;
    private const int kMaxIterationsPerRequest = 7;

    private static readonly List<TestCaseData> kInvalidSettings =
    [
        new TestCaseData("McpEndpoint", kRemoteHttpMcpEndpoint).SetName("RemoteHttpMcpEndpoint"),
        new TestCaseData("McpConnectionTimeout", "00:00:00").SetName("NoConnectionTimeout"),
        new TestCaseData("RunsPerMinute", "0").SetName("NoRuns"),
        new TestCaseData("MaxSessionsPerUser", "0").SetName("NoSessions"),
        new TestCaseData("MaxConcurrentRunsPerUser", "0").SetName("NoConcurrentRuns"),
        new TestCaseData("MaxConcurrentRunsPerUser", (HarnessSettings.kDefaultMaxSessionsPerUser + 1).ToString()).SetName("MoreConcurrentRunsThanSessions"),
        new TestCaseData("SessionIdleTimeout", "00:00:00").SetName("NoIdleTimeout"),
        new TestCaseData("SessionIdleTimeout", "-01:00:00").SetName("NegativeIdleTimeout")
    ];

    /// <summary>
    /// Verifies that without configuration the defaults apply: the MCP server on the same host, the default limits,
    /// and no provider, which the agent factory refuses later.
    /// </summary>
    [Test]
    public void FromConfiguration_WithoutSection_UsesDefaults()
    {
        HarnessSettings settings = HarnessSettings.FromConfiguration(CreateConfiguration(new Dictionary<string, string?>()));

        Assert.That(settings.McpEndpoint, Is.EqualTo(new Uri(HarnessSettings.kDefaultMcpEndpoint)));
        Assert.That(settings.McpConnectionTimeout, Is.Null);
        Assert.That(settings.RunsPerMinute, Is.EqualTo(HarnessSettings.kDefaultRunsPerMinute));
        Assert.That(settings.MaxSessionsPerUser, Is.EqualTo(HarnessSettings.kDefaultMaxSessionsPerUser));
        Assert.That(settings.MaxConcurrentRunsPerUser, Is.EqualTo(HarnessSettings.kDefaultMaxConcurrentRunsPerUser));
        Assert.That(settings.SessionIdleTimeout, Is.EqualTo(HarnessSettings.kDefaultSessionIdleTimeout));
        Assert.That(settings.Provider.Model, Is.Empty);
    }

    /// <summary>
    /// Verifies that the configured values are read, including the provider and agent settings, e.g. the API key from
    /// an environment variable.
    /// </summary>
    [Test]
    public void FromConfiguration_WithSection_ReadsValues()
    {
        Dictionary<string, string?> values = new()
        {
            ["Harness:McpEndpoint"] = kLoopbackMcpEndpoint,
            ["Harness:McpConnectionTimeout"] = kConnectionTimeout,
            ["Harness:RunsPerMinute"] = kRunsPerMinute.ToString(),
            ["Harness:MaxSessionsPerUser"] = kMaxSessionsPerUser.ToString(),
            ["Harness:MaxConcurrentRunsPerUser"] = kMaxConcurrentRunsPerUser.ToString(),
            ["Harness:SessionIdleTimeout"] = kIdleTimeout,
            ["Harness:Provider:Provider"] = nameof(AiProvider.Anthropic),
            ["Harness:Provider:Model"] = kModel,
            ["Harness:Provider:ApiKey"] = kApiKey,
            ["Harness:Provider:ContextWindowTokens"] = kContextWindowTokens.ToString(),
            ["Harness:Agent:MaxIterationsPerRequest"] = kMaxIterationsPerRequest.ToString()
        };

        HarnessSettings settings = HarnessSettings.FromConfiguration(CreateConfiguration(values));

        Assert.That(settings.McpEndpoint, Is.EqualTo(new Uri(kLoopbackMcpEndpoint)));
        Assert.That(settings.McpConnectionTimeout, Is.EqualTo(TimeSpan.Parse(kConnectionTimeout)));
        Assert.That(settings.RunsPerMinute, Is.EqualTo(kRunsPerMinute));
        Assert.That(settings.MaxSessionsPerUser, Is.EqualTo(kMaxSessionsPerUser));
        Assert.That(settings.MaxConcurrentRunsPerUser, Is.EqualTo(kMaxConcurrentRunsPerUser));
        Assert.That(settings.SessionIdleTimeout, Is.EqualTo(TimeSpan.Parse(kIdleTimeout)));
        Assert.That(settings.Provider.Provider, Is.EqualTo(AiProvider.Anthropic));
        Assert.That(settings.Provider.Model, Is.EqualTo(kModel));
        Assert.That(settings.Provider.ApiKey, Is.EqualTo(kApiKey));
        Assert.That(settings.Provider.ContextWindowTokens, Is.EqualTo(kContextWindowTokens));
        Assert.That(settings.Agent.MaxIterationsPerRequest, Is.EqualTo(kMaxIterationsPerRequest));
    }

    /// <summary>
    /// Verifies that invalid server settings are refused when they are read.
    /// </summary>
    [TestCaseSource(nameof(kInvalidSettings))]
    public void FromConfiguration_WithInvalidSetting_Throws(string key, string value)
    {
        Dictionary<string, string?> values = new() { [$"{HarnessSettings.kSectionName}:{key}"] = value };

        Assert.Catch<ArgumentException>(() => HarnessSettings.FromConfiguration(CreateConfiguration(values)));
    }

    /// <summary>
    /// Verifies that a missing configuration is refused.
    /// </summary>
    [Test]
    public void FromConfiguration_WithoutConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => HarnessSettings.FromConfiguration(null!));
    }

    /// <summary>
    /// Creates a configuration with the given values.
    /// </summary>
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
