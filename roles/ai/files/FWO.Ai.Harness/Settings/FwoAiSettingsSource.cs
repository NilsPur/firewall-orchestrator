using FWO.Data.Ai;
using FWO.Encryption;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Reads the AI settings of the UI and decrypts the API key from the credential they reference, with the main key of
/// the host. Settings that cannot be used are returned with their problem rather than thrown, so that they reach the
/// log and the failing runs; only a failure to read them at all is thrown.
/// </summary>
public sealed class FwoAiSettingsSource : IAiSettingsSource
{
    private readonly IAiConfigReader configReader;
    private readonly Func<string> mainKeyProvider;

    /// <summary>
    /// Creates the source.
    /// </summary>
    /// <param name="configReader">Reads the settings and the credential from the API</param>
    /// <param name="mainKeyProvider">Returns the main key the secrets are encrypted with; the main key file of the host if null</param>
    public FwoAiSettingsSource(IAiConfigReader configReader, Func<string>? mainKeyProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configReader);
        this.configReader = configReader;
        this.mainKeyProvider = mainKeyProvider ?? AesEnc.GetMainKey;
    }

    /// <summary>
    /// Reads the AI settings; for a provider requiring an API key also the credential and decrypts its secret.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reading</param>
    /// <exception cref="Exception">The settings or the credential could not be read, or the main key is missing.</exception>
    public async Task<AiRuntimeSettings> LoadAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string?> configValues = await configReader.ReadSettingsAsync(cancellationToken);
        AiSettings settings = AiSettings.FromConfigValues(configValues);
        if (!settings.IsConfigured)
        {
            return new AiRuntimeSettings(settings);
        }
        List<AiSettingsProblem> problems = settings.Validate();
        if (problems.Count > 0)
        {
            return new AiRuntimeSettings(settings, problem: $"The AI settings are invalid: {string.Join(", ", problems)}.");
        }
        if (!settings.RequiresApiKey)
        {
            return new AiRuntimeSettings(settings);
        }
        AiCredentialValue? credential = await configReader.ReadCredentialAsync(cancellationToken);
        return ResolveApiKey(settings, credential);
    }

    /// <summary>
    /// Decrypts the API key from the credential of the settings.
    /// </summary>
    /// <exception cref="Exception">The main key is missing.</exception>
    private AiRuntimeSettings ResolveApiKey(AiSettings settings, AiCredentialValue? credential)
    {
        if (credential == null || credential.Id != settings.CredentialId)
        {
            return new AiRuntimeSettings(settings, problem: $"The credential {settings.CredentialId} of the AI provider was not found.");
        }
        if (credential.IsKeyPair)
        {
            return new AiRuntimeSettings(settings, problem: $"The credential {credential.Id} of the AI provider is a key pair, it needs the API key as secret.");
        }
        bool decrypted = AesEnc.TryDecrypt(credential.Secret, mainKeyProvider(), out string apiKey);
        if (!decrypted || string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiRuntimeSettings(settings, problem: $"The secret of the credential {credential.Id} of the AI provider could not be decrypted.");
        }
        return new AiRuntimeSettings(settings, apiKey.Trim());
    }
}
