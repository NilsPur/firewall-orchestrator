using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Providers;
using FWO.Data.Ai;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// AI settings the harness runs its agent with: the settings of the AI settings page of the UI, the API key from the
/// credential they reference, or why they cannot be used.
/// A class rather than a record on purpose: the generated ToString of a record would print the API key.
/// </summary>
public sealed class AiRuntimeSettings
{
    /// <summary>
    /// Creates the runtime settings.
    /// </summary>
    /// <param name="values">Settings of the AI settings page</param>
    /// <param name="apiKey">API key in plain text, null if the provider needs none or it could not be read</param>
    /// <param name="problem">Why the settings cannot be used, null if they can; never contains the API key</param>
    public AiRuntimeSettings(AiSettings values, string? apiKey = null, string? problem = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = values;
        ApiKey = apiKey;
        Problem = problem;
    }

    /// <summary>Settings of the AI settings page.</summary>
    public AiSettings Values { get; }

    /// <summary>API key in plain text; null if the provider needs none. Never logged.</summary>
    public string? ApiKey { get; }

    /// <summary>Why the settings cannot be used; null if they can. Never contains the API key.</summary>
    public string? Problem { get; }

    /// <summary>Whether an AI provider is configured.</summary>
    public bool IsConfigured => Values.IsConfigured;

    /// <summary>
    /// Tells whether the other settings have the same values, the API key included, so that the agent factory can be
    /// kept.
    /// </summary>
    /// <param name="other">Settings to compare with; null for none</param>
    public bool HasSameValues(AiRuntimeSettings? other)
    {
        return other != null && GetComparableValues().Equals(other.GetComparableValues());
    }

    /// <summary>
    /// Returns the settings of the AI provider.
    /// </summary>
    /// <exception cref="ArgumentException">The endpoint is not an absolute URI.</exception>
    public AiProviderSettings ToProviderSettings()
    {
        return new AiProviderSettings
        {
            Provider = Values.Provider,
            Model = Values.Model,
            ApiKey = Values.RequiresApiKey ? ApiKey : null,
            Endpoint = ParseEndpoint(Values.Endpoint),
            ContextWindowTokens = Values.ContextWindowTokens,
            MaxOutputTokens = Values.MaxOutputTokens > 0 ? Values.MaxOutputTokens : null
        };
    }

    /// <summary>
    /// Returns the settings of the agent loop and the compaction.
    /// </summary>
    public AgentSettings ToAgentSettings()
    {
        return new AgentSettings
        {
            MaxIterationsPerRequest = Values.MaxIterationsPerRequest,
            MaxConsecutiveErrorsPerRequest = Values.MaxConsecutiveErrorsPerRequest,
            CompactionReserveTokens = Values.CompactionReserveTokens > 0 ? Values.CompactionReserveTokens : null
        };
    }

    /// <summary>
    /// Returns the endpoint, null for an empty one.
    /// </summary>
    /// <exception cref="ArgumentException">The endpoint is not an absolute URI; the endpoint is not part of the message.</exception>
    private static Uri? ParseEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }
        return Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? uri)
            ? uri
            : throw new ArgumentException("The AI provider endpoint must be an absolute http or https URI.", nameof(endpoint));
    }

    /// <summary>
    /// Returns all values that decide about the agent factory, for <see cref="HasSameValues"/>.
    /// </summary>
    private (AiProvider, string, string, int, int, int, int, int, int, string?, string?) GetComparableValues()
    {
        return (Values.Provider, Values.Model, Values.Endpoint, Values.CredentialId, Values.ContextWindowTokens, Values.MaxOutputTokens,
            Values.MaxIterationsPerRequest, Values.MaxConsecutiveErrorsPerRequest, Values.CompactionReserveTokens, ApiKey, Problem);
    }
}
