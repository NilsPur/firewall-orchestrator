using FWO.Data.Ai;

namespace FWO.Ai.Harness.Providers;

/// <summary>
/// Settings of the AI provider and model to use, taken from the AI settings of the UI (see
/// <see cref="Settings.AiRuntimeSettings"/>).
/// A class rather than a record on purpose: the generated ToString of a record would print the API key.
/// </summary>
public class AiProviderSettings
{
    /// <summary>Provider to talk to; <see cref="AiProvider.None"/> is refused when the chat client is created.</summary>
    public AiProvider Provider { get; set; }

    /// <summary>Model id, e.g. the model name known to the provider.</summary>
    public string Model { get; set; } = "";

    /// <summary>API key; required for all providers but Ollama. Never logged or put into error messages.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Custom base URL of the provider; the provider's default if not set.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Context window of the model in tokens; required and positive. The agent compacts its history by it, and every
    /// request to Ollama carries it as num_ctx, as Ollama otherwise cuts the prompt at its own, often much smaller, default.
    /// </summary>
    public int ContextWindowTokens { get; set; }

    /// <summary>
    /// Default maximum number of output tokens per response; the provider's default if not set.
    /// Must be smaller than <see cref="ContextWindowTokens"/>.
    /// </summary>
    public int? MaxOutputTokens { get; set; }
}
