using FWO.Ai.Harness.Settings;
using FWO.Data.Ai;
using FWO.Encryption;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests reading the AI settings of the UI: the credential is only read for a provider needing an API key, its secret
/// is decrypted with the main key, and settings that cannot be used come back with their problem, which never holds the
/// API key.
/// </summary>
[TestFixture]
internal class HarnessFwoAiSettingsSourceTest
{
    private const string kMainKey = "0123456789abcdef0123456789abcdef";
    private const string kTooShortSecret = "c2hvcnQ=";
    private const string kApiKey = "sk-source-test-key-4711";
    private const string kModel = "test-model";
    private const int kCredentialId = 12;
    private const int kOtherCredentialId = 13;

    /// <summary>
    /// Verifies that settings without provider are returned as not configured, without reading the credential.
    /// </summary>
    [Test]
    public async Task LoadAsync_WithoutProvider_IsNotConfigured()
    {
        FakeAiConfigReader reader = new();

        AiRuntimeSettings settings = await CreateSource(reader).LoadAsync(CancellationToken.None);

        Assert.That(settings.IsConfigured, Is.False);
        Assert.That(settings.Problem, Is.Null);
        Assert.That(reader.CredentialReads, Is.Zero);
    }

    /// <summary>
    /// Verifies that invalid settings come back with all their problems, without reading the credential.
    /// </summary>
    [Test]
    public async Task LoadAsync_WithInvalidSettings_ReturnsProblems()
    {
        FakeAiConfigReader reader = CreateReader(AiProvider.OpenAi);
        reader.ConfigValues[AiSettings.kModelKey] = "";
        reader.ConfigValues[AiSettings.kContextWindowTokensKey] = "0";

        AiRuntimeSettings settings = await CreateSource(reader).LoadAsync(CancellationToken.None);

        Assert.That(settings.Problem, Does.Contain(nameof(AiSettingsProblem.ModelMissing)));
        Assert.That(settings.Problem, Does.Contain(nameof(AiSettingsProblem.ContextWindowInvalid)));
        Assert.That(reader.CredentialReads, Is.Zero);
    }

    /// <summary>
    /// Verifies that Ollama needs no credential, even if one is selected.
    /// </summary>
    [Test]
    public async Task LoadAsync_WithOllama_ReadsNoCredential()
    {
        FakeAiConfigReader reader = CreateReader(AiProvider.Ollama);
        reader.ConfigValues[AiSettings.kCredentialIdKey] = kCredentialId.ToString();

        AiRuntimeSettings settings = await CreateSource(reader).LoadAsync(CancellationToken.None);

        Assert.That(settings.IsConfigured, Is.True);
        Assert.That(settings.Problem, Is.Null);
        Assert.That(settings.ApiKey, Is.Null);
        Assert.That(reader.CredentialReads, Is.Zero);
    }

    /// <summary>
    /// Verifies that the API key is decrypted from the secret of the selected credential.
    /// </summary>
    [Test]
    public async Task LoadAsync_WithCredential_DecryptsApiKey()
    {
        FakeAiConfigReader reader = CreateReader(AiProvider.Anthropic);
        reader.Credential = new AiCredentialValue { Id = kCredentialId, Secret = AesEnc.Encrypt(kApiKey, kMainKey) };

        AiRuntimeSettings settings = await CreateSource(reader).LoadAsync(CancellationToken.None);

        Assert.That(settings.Problem, Is.Null);
        Assert.That(settings.ApiKey, Is.EqualTo(kApiKey));
        Assert.That(settings.Values.Provider, Is.EqualTo(AiProvider.Anthropic));
        Assert.That(settings.Values.Model, Is.EqualTo(kModel));
    }

    /// <summary>
    /// Verifies that a missing credential, another credential, a key pair, and a secret that cannot be decrypted are
    /// reported as problems, never containing the API key.
    /// </summary>
    [Test]
    public async Task LoadAsync_WithUnusableCredential_ReturnsProblem()
    {
        string encryptedApiKey = AesEnc.Encrypt(kApiKey, kMainKey);
        List<AiCredentialValue?> unusableCredentials =
        [
            null,
            new AiCredentialValue { Id = kOtherCredentialId, Secret = encryptedApiKey },
            new AiCredentialValue { Id = kCredentialId, IsKeyPair = true, Secret = encryptedApiKey },
            new AiCredentialValue { Id = kCredentialId, Secret = kTooShortSecret },
            new AiCredentialValue { Id = kCredentialId, Secret = kApiKey }
        ];

        foreach (AiCredentialValue? credential in unusableCredentials)
        {
            FakeAiConfigReader reader = CreateReader(AiProvider.OpenAi);
            reader.Credential = credential;

            AiRuntimeSettings settings = await CreateSource(reader).LoadAsync(CancellationToken.None);

            Assert.That(settings.Problem, Is.Not.Null.And.Contains(kCredentialId.ToString()));
            Assert.That(settings.Problem, Does.Not.Contain(kApiKey));
            Assert.That(settings.ApiKey, Is.Null);
        }
    }

    /// <summary>
    /// Verifies that a failure to read the settings is passed on, so that the current settings stay in effect.
    /// </summary>
    [Test]
    public void LoadAsync_WhenReadingFails_Throws()
    {
        FakeAiConfigReader reader = new() { Failure = new HttpRequestException("API not reachable") };

        Assert.ThrowsAsync<HttpRequestException>(() => CreateSource(reader).LoadAsync(CancellationToken.None));
    }

    /// <summary>
    /// Verifies that the source needs a reader.
    /// </summary>
    [Test]
    public void Ctor_WithoutReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new FwoAiSettingsSource(null!));
    }

    /// <summary>
    /// Creates the source decrypting with the test main key.
    /// </summary>
    private static FwoAiSettingsSource CreateSource(FakeAiConfigReader reader)
    {
        return new FwoAiSettingsSource(reader, () => kMainKey);
    }

    /// <summary>
    /// Creates a reader with valid settings of the given provider, referencing the test credential.
    /// </summary>
    private static FakeAiConfigReader CreateReader(AiProvider provider)
    {
        FakeAiConfigReader reader = new();
        reader.ConfigValues[AiSettings.kProviderKey] = provider.ToString();
        reader.ConfigValues[AiSettings.kModelKey] = kModel;
        reader.ConfigValues[AiSettings.kCredentialIdKey] = kCredentialId.ToString();
        reader.ConfigValues[AiSettings.kContextWindowTokensKey] = HarnessTestSetup.kContextWindowTokens.ToString();
        return reader;
    }
}
