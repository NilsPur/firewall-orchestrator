using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Providers;
using FWO.Ai.Harness.Settings;
using FWO.Data.Ai;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the runtime AI settings: how they map to the settings of the provider and the agent, and when two of them are
/// the same, so that the agent factory is kept.
/// </summary>
[TestFixture]
internal class HarnessAiRuntimeSettingsTest
{
    private const string kModel = "test-model";
    private const string kApiKey = "sk-runtime-test-key-4711";
    private const string kOtherApiKey = "sk-runtime-test-key-0815";
    private const string kEndpoint = "https://llm.example.com/v1";
    private const int kContextWindowTokens = 100000;
    private const int kMaxOutputTokens = 4000;
    private const int kMaxIterations = 6;
    private const int kMaxConsecutiveErrors = 2;
    private const int kCompactionReserveTokens = 30000;
    private const int kCredentialId = 7;

    /// <summary>
    /// Verifies that all values are passed on to the provider and agent settings.
    /// </summary>
    [Test]
    public void ToProviderAndAgentSettings_PassValuesOn()
    {
        AiRuntimeSettings runtimeSettings = new(CreateSettings(AiProvider.OpenAi), kApiKey);

        AiProviderSettings providerSettings = runtimeSettings.ToProviderSettings();
        AgentSettings agentSettings = runtimeSettings.ToAgentSettings();

        Assert.That(providerSettings.Provider, Is.EqualTo(AiProvider.OpenAi));
        Assert.That(providerSettings.Model, Is.EqualTo(kModel));
        Assert.That(providerSettings.ApiKey, Is.EqualTo(kApiKey));
        Assert.That(providerSettings.Endpoint, Is.EqualTo(new Uri(kEndpoint)));
        Assert.That(providerSettings.ContextWindowTokens, Is.EqualTo(kContextWindowTokens));
        Assert.That(providerSettings.MaxOutputTokens, Is.EqualTo(kMaxOutputTokens));
        Assert.That(agentSettings.MaxIterationsPerRequest, Is.EqualTo(kMaxIterations));
        Assert.That(agentSettings.MaxConsecutiveErrorsPerRequest, Is.EqualTo(kMaxConsecutiveErrors));
        Assert.That(agentSettings.CompactionReserveTokens, Is.EqualTo(kCompactionReserveTokens));
    }

    /// <summary>
    /// Verifies that 0 and empty values stand for the defaults, and that Ollama gets no API key.
    /// </summary>
    [Test]
    public void ToProviderAndAgentSettings_WithDefaults_LeaveThemUnset()
    {
        AiSettings settings = CreateSettings(AiProvider.Ollama);
        settings.Endpoint = "";
        settings.MaxOutputTokens = 0;
        settings.CompactionReserveTokens = 0;
        AiRuntimeSettings runtimeSettings = new(settings, kApiKey);

        AiProviderSettings providerSettings = runtimeSettings.ToProviderSettings();
        AgentSettings agentSettings = runtimeSettings.ToAgentSettings();

        Assert.That(providerSettings.ApiKey, Is.Null);
        Assert.That(providerSettings.Endpoint, Is.Null);
        Assert.That(providerSettings.MaxOutputTokens, Is.Null);
        Assert.That(agentSettings.CompactionReserveTokens, Is.Null);
    }

    /// <summary>
    /// Verifies that an endpoint that is not an absolute URI is refused without naming it.
    /// </summary>
    [Test]
    public void ToProviderSettings_WithRelativeEndpoint_Throws()
    {
        AiSettings settings = CreateSettings(AiProvider.OpenAi);
        settings.Endpoint = "llm/v1";

        ArgumentException? exception = Assert.Throws<ArgumentException>(() => new AiRuntimeSettings(settings, kApiKey).ToProviderSettings());

        Assert.That(exception!.Message, Does.Not.Contain(settings.Endpoint));
    }

    /// <summary>
    /// Verifies that settings with the same values are the same, and that a changed API key, value or problem is not.
    /// </summary>
    [Test]
    public void HasSameValues_ComparesAllValues()
    {
        AiRuntimeSettings settings = new(CreateSettings(AiProvider.OpenAi), kApiKey);
        AiSettings changedModel = CreateSettings(AiProvider.OpenAi);
        changedModel.Model = "other-model";

        Assert.That(settings.HasSameValues(new AiRuntimeSettings(CreateSettings(AiProvider.OpenAi), kApiKey)), Is.True);
        Assert.That(settings.HasSameValues(new AiRuntimeSettings(CreateSettings(AiProvider.OpenAi), kOtherApiKey)), Is.False);
        Assert.That(settings.HasSameValues(new AiRuntimeSettings(changedModel, kApiKey)), Is.False);
        Assert.That(settings.HasSameValues(new AiRuntimeSettings(CreateSettings(AiProvider.OpenAi), kApiKey, "problem")), Is.False);
        Assert.That(settings.HasSameValues(null), Is.False);
    }

    /// <summary>
    /// Verifies that the API key does not show in the text of the settings, e.g. in a log message.
    /// </summary>
    [Test]
    public void ToString_DoesNotContainApiKey()
    {
        AiRuntimeSettings settings = new(CreateSettings(AiProvider.OpenAi), kApiKey);

        Assert.That(settings.ToString(), Does.Not.Contain(kApiKey));
        Assert.That(settings.ToProviderSettings().ToString(), Does.Not.Contain(kApiKey));
    }

    /// <summary>
    /// Creates valid settings of the given provider.
    /// </summary>
    private static AiSettings CreateSettings(AiProvider provider)
    {
        return new AiSettings
        {
            Provider = provider,
            Model = kModel,
            Endpoint = kEndpoint,
            CredentialId = kCredentialId,
            ContextWindowTokens = kContextWindowTokens,
            MaxOutputTokens = kMaxOutputTokens,
            MaxIterationsPerRequest = kMaxIterations,
            MaxConsecutiveErrorsPerRequest = kMaxConsecutiveErrors,
            CompactionReserveTokens = kCompactionReserveTokens
        };
    }
}
