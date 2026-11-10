using Anthropic;
using FWO.Data.Ai;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using System.ClientModel;
using OllamaOption = OllamaSharp.Models.OllamaOption;

namespace FWO.Ai.Harness.Providers;

/// <summary>
/// Creates the Microsoft.Extensions.AI chat client of an AI provider.
/// </summary>
public static class ChatClientFactory
{
    /// <summary>Endpoint of a local Ollama server, used if no endpoint is given.</summary>
    internal const string kDefaultOllamaEndpoint = "http://localhost:11434";

    private const string kProviderEndpointPurpose = "AI provider endpoint";

    /// <summary>
    /// Creates the chat client of the configured provider. Disposing the returned client frees the SDK client too.
    /// </summary>
    /// <param name="settings">Provider, model, credentials and token limits</param>
    /// <exception cref="ArgumentException">The settings are incomplete or invalid; the message never contains the API key.</exception>
    public static IChatClient Create(AiProviderSettings settings)
    {
        Validate(settings);
        IChatClient providerClient = settings.Provider switch
        {
            AiProvider.Ollama => CreateOllamaClient(settings),
            AiProvider.OpenAi => CreateOpenAiClient(settings),
            AiProvider.Anthropic => CreateAnthropicClient(settings),
            _ => CreateGoogleClient(settings)
        };
        return AddRequestDefaults(providerClient, settings);
    }

    /// <summary>
    /// Checks the settings before any client is created.
    /// </summary>
    /// <exception cref="ArgumentException">The settings are incomplete or invalid.</exception>
    internal static void Validate(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Provider))
        {
            throw new ArgumentException("Unknown AI provider.", nameof(settings));
        }
        if (settings.Provider == AiProvider.None)
        {
            throw new ArgumentException("No AI provider is configured.", nameof(settings));
        }
        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new ArgumentException("A model is required.", nameof(settings));
        }
        ValidateTokenLimits(settings);
        bool sendsApiKey = settings.Provider != AiProvider.Ollama;
        if (sendsApiKey && string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new ArgumentException($"An API key is required for {settings.Provider}.", nameof(settings));
        }
        if (settings.Endpoint == null)
        {
            return;
        }
        if (sendsApiKey)
        {
            EndpointGuard.RequireHttpsOrLoopback(settings.Endpoint, kProviderEndpointPurpose);
        }
        else
        {
            EndpointGuard.RequireHttpOrHttps(settings.Endpoint, kProviderEndpointPurpose);
        }
    }

    /// <summary>
    /// Checks the context window and the maximum number of output tokens, which has to fit into the context window.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A token limit is not positive, or the output does not fit.</exception>
    private static void ValidateTokenLimits(AiProviderSettings settings)
    {
        if (settings.ContextWindowTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "The context window must be positive.");
        }
        if (settings.MaxOutputTokens is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "The maximum number of output tokens must be positive.");
        }
        if (settings.MaxOutputTokens is int maxOutputTokens && maxOutputTokens >= settings.ContextWindowTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "The maximum number of output tokens must be smaller than the context window.");
        }
    }

    /// <summary>
    /// Adds the defaults every request gets unless it sets them itself: the maximum number of output tokens, if
    /// configured, and for Ollama the context window. Returns the provider client unchanged if there is no default.
    /// </summary>
    private static IChatClient AddRequestDefaults(IChatClient providerClient, AiProviderSettings settings)
    {
        int? maxOutputTokens = settings.MaxOutputTokens;
        int? ollamaContextWindowTokens = settings.Provider == AiProvider.Ollama ? settings.ContextWindowTokens : null;
        if (maxOutputTokens == null && ollamaContextWindowTokens == null)
        {
            return providerClient;
        }
        return new ChatClientBuilder(providerClient)
            .ConfigureOptions(options => ApplyRequestDefaults(options, maxOutputTokens, ollamaContextWindowTokens))
            .Build();
    }

    /// <summary>
    /// Sets the defaults on the options of a request (a copy made by the options configuring client), keeping what the
    /// request sets itself.
    /// </summary>
    /// <param name="options">Options of the request</param>
    /// <param name="maxOutputTokens">Default maximum number of output tokens; none if null</param>
    /// <param name="ollamaContextWindowTokens">Context window sent to Ollama as num_ctx; none if null</param>
    private static void ApplyRequestDefaults(ChatOptions options, int? maxOutputTokens, int? ollamaContextWindowTokens)
    {
        options.MaxOutputTokens ??= maxOutputTokens;
        if (ollamaContextWindowTokens is int numCtx && !HasOllamaOption(options, OllamaOption.NumCtx))
        {
            options.AddOllamaOption(OllamaOption.NumCtx, numCtx);
        }
    }

    /// <summary>
    /// Tells whether the options already set an Ollama option; OllamaSharp ignores an option whose value is null.
    /// </summary>
    private static bool HasOllamaOption(ChatOptions options, OllamaOption option)
    {
        object? value = null;
        bool isSet = options.AdditionalProperties?.TryGetValue(option.Name, out value) == true;
        return isSet && value != null;
    }

    /// <summary>
    /// Returns the API key of a provider that requires one, checked by <see cref="Validate"/> before.
    /// </summary>
    /// <exception cref="ArgumentException">There is no API key.</exception>
    private static string GetApiKey(AiProviderSettings settings)
    {
        return settings.ApiKey ?? throw new ArgumentException($"An API key is required for {settings.Provider}.", nameof(settings));
    }

    /// <summary>
    /// Creates an Ollama client; the OllamaSharp client is a chat client itself.
    /// </summary>
    private static IChatClient CreateOllamaClient(AiProviderSettings settings)
    {
        Uri endpoint = settings.Endpoint ?? new Uri(kDefaultOllamaEndpoint);
        return new OllamaApiClient(endpoint, settings.Model);
    }

    /// <summary>
    /// Creates an OpenAI client, for a custom OpenAI compatible endpoint if one is given.
    /// </summary>
    private static IChatClient CreateOpenAiClient(AiProviderSettings settings)
    {
        OpenAIClientOptions clientOptions = new();
        if (settings.Endpoint != null)
        {
            clientOptions.Endpoint = settings.Endpoint;
        }
        OpenAIClient openAiClient = new(new ApiKeyCredential(GetApiKey(settings)), clientOptions);
        return openAiClient.GetChatClient(settings.Model).AsIChatClient();
    }

    /// <summary>
    /// Creates an Anthropic client, for a custom base URL if one is given.
    /// </summary>
    private static IChatClient CreateAnthropicClient(AiProviderSettings settings)
    {
        // BaseUrl is init-only and must only be set if a custom one is given, otherwise the SDK default applies
        string apiKey = GetApiKey(settings);
        AnthropicClient anthropicClient = settings.Endpoint == null
            ? new AnthropicClient { ApiKey = apiKey }
            : new AnthropicClient { ApiKey = apiKey, BaseUrl = settings.Endpoint.ToString() };
        IChatClient chatClient = anthropicClient.AsIChatClient(settings.Model, settings.MaxOutputTokens);
        return new ProviderChatClient(chatClient, anthropicClient);
    }

    /// <summary>
    /// Creates a Google client for the Gemini Developer API (not Vertex AI), for a custom base URL if one is given.
    /// </summary>
    private static IChatClient CreateGoogleClient(AiProviderSettings settings)
    {
        HttpOptions? httpOptions = null;
        if (settings.Endpoint != null)
        {
            httpOptions = new HttpOptions { BaseUrl = settings.Endpoint.ToString() };
        }
        Google.GenAI.Client googleClient = new(vertexAI: false, apiKey: GetApiKey(settings), httpOptions: httpOptions);
        IChatClient chatClient = googleClient.AsIChatClient(settings.Model);
        return new ProviderChatClient(chatClient, googleClient);
    }
}
