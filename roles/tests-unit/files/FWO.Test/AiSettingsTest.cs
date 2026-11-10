using FWO.Config.Api.Data;
using FWO.Data.Ai;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the AI settings of the AI settings page: reading them from the global config items the way the AI harness does,
/// checking them, and storing them in the global config.
/// </summary>
[TestFixture]
[Parallelizable]
internal class AiSettingsTest
{
    private const string kModel = "gpt-test";
    private const string kHttpsEndpoint = "https://llm.example.com/v1";
    private const int kCredentialId = 4;
    private const int kContextWindowTokens = 100000;
    private static readonly List<AiSettingsProblem> kCredentialMissing = [AiSettingsProblem.CredentialMissing];

    /// <summary>
    /// Verifies that all items are read, the provider ignoring case and the texts trimmed.
    /// </summary>
    [Test]
    public void FromConfigValues_ReadsAllItems()
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kProviderKey] = "anthropic",
            [AiSettings.kModelKey] = $" {kModel} ",
            [AiSettings.kEndpointKey] = $" {kHttpsEndpoint} ",
            [AiSettings.kCredentialIdKey] = "4",
            [AiSettings.kContextWindowTokensKey] = "100000",
            [AiSettings.kMaxOutputTokensKey] = "4000",
            [AiSettings.kMaxIterationsPerRequestKey] = "7",
            [AiSettings.kMaxConsecutiveErrorsPerRequestKey] = "2",
            [AiSettings.kCompactionReserveTokensKey] = "30000"
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Provider, Is.EqualTo(AiProvider.Anthropic));
            Assert.That(settings.Model, Is.EqualTo(kModel));
            Assert.That(settings.Endpoint, Is.EqualTo(kHttpsEndpoint));
            Assert.That(settings.CredentialId, Is.EqualTo(kCredentialId));
            Assert.That(settings.ContextWindowTokens, Is.EqualTo(kContextWindowTokens));
            Assert.That(settings.MaxOutputTokens, Is.EqualTo(4000));
            Assert.That(settings.MaxIterationsPerRequest, Is.EqualTo(7));
            Assert.That(settings.MaxConsecutiveErrorsPerRequest, Is.EqualTo(2));
            Assert.That(settings.CompactionReserveTokens, Is.EqualTo(30000));
            Assert.That(settings.Validate(), Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that missing, empty, unknown and numeric values fall back to the defaults.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("ChatBot")]
    [TestCase("2")]
    [TestCase("99")]
    public void FromConfigValues_WithUnreadableValues_UsesDefaults(string? value)
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kProviderKey] = value,
            [AiSettings.kMaxIterationsPerRequestKey] = value,
            [AiSettings.kMaxConsecutiveErrorsPerRequestKey] = "three"
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Provider, Is.EqualTo(AiProvider.None));
            Assert.That(settings.IsConfigured, Is.False);
            Assert.That(settings.MaxIterationsPerRequest, Is.EqualTo(value is "2" or "99" ? int.Parse(value) : AiSettings.kDefaultMaxIterationsPerRequest));
            Assert.That(settings.MaxConsecutiveErrorsPerRequest, Is.EqualTo(AiSettings.kDefaultMaxConsecutiveErrorsPerRequest));
            Assert.That(settings.Model, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that settings without provider are not checked, as they are not used.
    /// </summary>
    [Test]
    public void Validate_WithoutProvider_HasNoProblems()
    {
        AiSettings settings = new() { ContextWindowTokens = -1, MaxIterationsPerRequest = 0 };

        Assert.That(settings.Validate(), Is.Empty);
    }

    /// <summary>
    /// Verifies that a provider other than Ollama needs a credential, and Ollama does not.
    /// </summary>
    [Test]
    public void Validate_RequiresCredentialForApiKeyProvidersOnly()
    {
        AiSettings openAi = CreateValidSettings(AiProvider.OpenAi);
        openAi.CredentialId = 0;
        AiSettings ollama = CreateValidSettings(AiProvider.Ollama);
        ollama.CredentialId = 0;

        Assert.Multiple(() =>
        {
            Assert.That(openAi.Validate(), Is.EqualTo(kCredentialMissing));
            Assert.That(openAi.RequiresApiKey, Is.True);
            Assert.That(ollama.Validate(), Is.Empty);
            Assert.That(ollama.RequiresApiKey, Is.False);
        });
    }

    /// <summary>
    /// Verifies the checks of the endpoint: an absolute http or https URL, and https if the API key is sent to a host
    /// other than the local one.
    /// </summary>
    [TestCase(AiProvider.OpenAi, kHttpsEndpoint, null)]
    [TestCase(AiProvider.OpenAi, "http://127.0.0.1:8000/v1", null)]
    [TestCase(AiProvider.OpenAi, "http://localhost:8000/v1", null)]
    [TestCase(AiProvider.OpenAi, "http://llm.example.com/v1", AiSettingsProblem.EndpointNotSecure)]
    [TestCase(AiProvider.Ollama, "http://gpu-host.example.com:11434", null)]
    [TestCase(AiProvider.OpenAi, "llm.example.com/v1", AiSettingsProblem.EndpointInvalid)]
    [TestCase(AiProvider.OpenAi, "ftp://llm.example.com/v1", AiSettingsProblem.EndpointInvalid)]
    [TestCase(AiProvider.OpenAi, "javascript:alert(1)", AiSettingsProblem.EndpointInvalid)]
    public void Validate_ChecksEndpoint(AiProvider provider, string endpoint, AiSettingsProblem? expectedProblem)
    {
        AiSettings settings = CreateValidSettings(provider);
        settings.Endpoint = endpoint;

        List<AiSettingsProblem> problems = settings.Validate();

        Assert.That(problems, expectedProblem == null ? Is.Empty : Has.Count.EqualTo(1).And.Contains(expectedProblem.Value));
    }

    /// <summary>
    /// Verifies the checks of the token limits and the agent loop.
    /// </summary>
    [TestCase(0, 0, 10, 3, 0, AiSettingsProblem.ContextWindowInvalid)]
    [TestCase(100000, -1, 10, 3, 0, AiSettingsProblem.MaxOutputTokensInvalid)]
    [TestCase(100000, 100000, 10, 3, 0, AiSettingsProblem.MaxOutputTokensInvalid)]
    [TestCase(100000, 0, 0, 3, 0, AiSettingsProblem.MaxIterationsInvalid)]
    [TestCase(100000, 0, 10, -1, 0, AiSettingsProblem.MaxConsecutiveErrorsInvalid)]
    [TestCase(100000, 0, 10, 3, -1, AiSettingsProblem.CompactionReserveInvalid)]
    [TestCase(100000, 0, 10, 3, 100000, AiSettingsProblem.CompactionReserveInvalid)]
    [TestCase(100000, 30000, 10, 3, 20000, AiSettingsProblem.CompactionReserveInvalid)]
    [TestCase(100000, 30000, 10, 3, 0, AiSettingsProblem.CompactionReserveInvalid)]
    [TestCase(100000, 20000, 10, 3, 0, null)]
    [TestCase(100000, 30000, 10, 0, 40000, null)]
    public void Validate_ChecksLimits(int contextWindow, int maxOutput, int maxIterations, int maxErrors, int reserve,
        AiSettingsProblem? expectedProblem)
    {
        AiSettings settings = CreateValidSettings(AiProvider.OpenAi);
        settings.ContextWindowTokens = contextWindow;
        settings.MaxOutputTokens = maxOutput;
        settings.MaxIterationsPerRequest = maxIterations;
        settings.MaxConsecutiveErrorsPerRequest = maxErrors;
        settings.CompactionReserveTokens = reserve;

        List<AiSettingsProblem> problems = settings.Validate();

        Assert.That(problems, expectedProblem == null ? Is.Empty : Does.Contain(expectedProblem.Value));
    }

    /// <summary>
    /// Verifies that the compaction reserve is a quarter of the context window unless it is set.
    /// </summary>
    [Test]
    public void GetEffectiveCompactionReserveTokens_DefaultsToQuarterOfContextWindow()
    {
        AiSettings settings = CreateValidSettings(AiProvider.OpenAi);

        int defaultReserve = settings.GetEffectiveCompactionReserveTokens();
        settings.CompactionReserveTokens = 12345;

        Assert.That(defaultReserve, Is.EqualTo(kContextWindowTokens / AiSettings.kDefaultCompactionReserveDivisor));
        Assert.That(settings.GetEffectiveCompactionReserveTokens(), Is.EqualTo(12345));
    }

    /// <summary>
    /// Verifies that the AI settings are stored in the global config, with trimmed texts, and read back unchanged.
    /// </summary>
    [Test]
    public void ConfigExtensions_StoreAndReadSettings()
    {
        ConfigData config = new();
        AiSettings settings = CreateValidSettings(AiProvider.Google);
        settings.Model = $"  {kModel}\t";
        settings.Endpoint = $" {kHttpsEndpoint} ";
        settings.MaxOutputTokens = 2000;
        settings.MaxIterationsPerRequest = 5;
        settings.MaxConsecutiveErrorsPerRequest = 1;
        settings.CompactionReserveTokens = 25000;

        config.SetAiSettings(settings);
        AiSettings readBack = config.GetAiSettings();

        Assert.Multiple(() =>
        {
            Assert.That(config.AiModel, Is.EqualTo(kModel));
            Assert.That(config.AiEndpoint, Is.EqualTo(kHttpsEndpoint));
            Assert.That(readBack.Provider, Is.EqualTo(AiProvider.Google));
            Assert.That(readBack.Model, Is.EqualTo(kModel));
            Assert.That(readBack.CredentialId, Is.EqualTo(kCredentialId));
            Assert.That(readBack.ContextWindowTokens, Is.EqualTo(kContextWindowTokens));
            Assert.That(readBack.MaxOutputTokens, Is.EqualTo(2000));
            Assert.That(readBack.MaxIterationsPerRequest, Is.EqualTo(5));
            Assert.That(readBack.MaxConsecutiveErrorsPerRequest, Is.EqualTo(1));
            Assert.That(readBack.CompactionReserveTokens, Is.EqualTo(25000));
            Assert.That(readBack, Is.Not.SameAs(settings));
        });
    }

    /// <summary>
    /// Verifies that the defaults of the global config are those of the AI settings, so that an installation without
    /// stored AI settings behaves like the harness expects.
    /// </summary>
    [Test]
    public void ConfigData_DefaultsMatchAiSettings()
    {
        AiSettings fromConfig = new ConfigData().GetAiSettings();
        AiSettings defaults = new();

        Assert.Multiple(() =>
        {
            Assert.That(fromConfig.Provider, Is.EqualTo(defaults.Provider));
            Assert.That(fromConfig.MaxIterationsPerRequest, Is.EqualTo(defaults.MaxIterationsPerRequest));
            Assert.That(fromConfig.MaxConsecutiveErrorsPerRequest, Is.EqualTo(defaults.MaxConsecutiveErrorsPerRequest));
            Assert.That(fromConfig.CompactionReserveTokens, Is.EqualTo(defaults.CompactionReserveTokens));
        });
    }

    /// <summary>
    /// Verifies that only the referenced credential counts as used, and no credential if none is referenced.
    /// </summary>
    [Test]
    public void AiSettingsUseCredential_MatchesReferencedCredentialOnly()
    {
        ConfigData config = new() { AiCredentialId = kCredentialId };
        ConfigData withoutCredential = new();

        Assert.Multiple(() =>
        {
            Assert.That(config.AiSettingsUseCredential(kCredentialId), Is.True);
            Assert.That(config.AiSettingsUseCredential(kCredentialId + 1), Is.False);
            Assert.That(withoutCredential.AiSettingsUseCredential(0), Is.False);
        });
    }

    /// <summary>
    /// Verifies that every problem has its own error text, and that an unknown problem is refused.
    /// </summary>
    [Test]
    public void ProblemTexts_MapEachProblemToOwnText()
    {
        List<string> textKeys = [.. Enum.GetValues<AiSettingsProblem>().Select(AiSettingsProblemTexts.GetTextKey)];

        Assert.That(textKeys, Is.Unique);
        Assert.That(textKeys, Has.All.StartsWith("E53"));
        Assert.Throws<ArgumentOutOfRangeException>(() => AiSettingsProblemTexts.GetTextKey((AiSettingsProblem)999));
    }

    /// <summary>
    /// Creates valid settings of the given provider.
    /// </summary>
    private static AiSettings CreateValidSettings(AiProvider provider)
    {
        return new AiSettings
        {
            Provider = provider,
            Model = kModel,
            CredentialId = kCredentialId,
            ContextWindowTokens = kContextWindowTokens
        };
    }
}
