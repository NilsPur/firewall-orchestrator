namespace FWO.Ai.Harness.Providers;

/// <summary>
/// AI providers the harness can talk to, each through its own Microsoft.Extensions.AI chat client.
/// </summary>
public enum AiProvider
{
    /// <summary>Ollama, usually a local model server; no API key.</summary>
    Ollama,

    /// <summary>OpenAI or an OpenAI compatible endpoint.</summary>
    OpenAi,

    /// <summary>Anthropic (Claude).</summary>
    Anthropic,

    /// <summary>Google Gemini (Gemini Developer API).</summary>
    Google
}
