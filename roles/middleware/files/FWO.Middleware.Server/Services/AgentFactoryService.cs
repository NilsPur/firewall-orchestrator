using Anthropic;
using Anthropic.Core;
using FWO.Data.Ai;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Builds and caches the Microsoft Agent Framework agent for the fixed FWO assistant model.
    /// </summary>
    public class AgentFactoryService(AiToolExecutionService toolExecutionService, AiSettingsService settingsService)
    {
        /// <summary>
        /// Dependency-injection key and AG-UI agent name for the FWO assistant agent.
        /// </summary>
        public const string AgentName = "fwo-assistant";

        private readonly AiToolExecutionService toolExecutionService = toolExecutionService;
        private readonly AiSettingsService settingsService = settingsService;
        private readonly ConcurrentDictionary<string, AIAgent> agentCache = new();

        /// <summary>
        /// Returns the configured assistant agent, building it on first use.
        /// </summary>
        /// <exception cref="InvalidOperationException">No enabled AI model is configured.</exception>
        public async Task<AIAgent> GetAgent()
        {
            AiSettings settings = await settingsService.GetSettings();
            (AiProviderConfig Provider, AiModelConfig Model) selection = AiSettingsService.ResolveModel(settings)
                ?? throw new InvalidOperationException("No enabled AI model is configured.");
            return agentCache.GetOrAdd(CacheKey(selection.Provider, selection.Model, settings.SystemPrompt),
                _ => BuildAgent(selection.Provider, selection.Model, toolExecutionService.CreateTools(), settings.SystemPrompt));
        }

        /// <summary>
        /// Builds a fresh agent for the given provider and model without caching.
        /// </summary>
        public AIAgent BuildAgent(AiProviderConfig provider, AiModelConfig model, IList<AITool>? tools, string systemPrompt = "")
        {
            ChatClientAgentOptions options = new()
            {
                Name = AgentName,
                Description = "Firewall Orchestrator assistant.",
                ChatOptions = BuildChatOptions(model, tools, systemPrompt)
            };

            IChatClient chatClient = CreateLazyChatClient(provider, model);

            // Create ai agent with max output tokens, tools, temperature, top k / p, reasoning
            return chatClient.AsAIAgent(options);
        }

        /// <summary>
        /// Cache key that changes whenever a generation-relevant model setting changes, so edited
        /// configurations rebuild their agent instead of reusing a stale one.
        /// </summary>
        private static string CacheKey(AiProviderConfig provider, AiModelConfig model, string systemPrompt)
        {
            return string.Join("|", provider.Id, provider.Kind, model.ModelId, provider.EndpointUrl, provider.ApiKeyEnvVariable,
                model.Temperature, model.TopP, model.TopK, model.MaxOutputTokens, model.ReasoningEffort, provider.TimeoutSeconds,
                provider.MaxRetries, model.ToolCallsSupported, systemPrompt);
        }

        internal static ChatOptions BuildChatOptions(AiModelConfig model, IList<AITool>? tools, string systemPrompt = "")
        {
            // Tool calling is model-dependent, so fixed model capabilities decide whether tools are attached.
            IList<AITool>? effectiveTools = model.ToolCallsSupported == true ? tools : null;
            ChatOptions options = new()
            {
                Instructions = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
                MaxOutputTokens = model.MaxOutputTokens,
                Temperature = model.Temperature,
                TopP = model.TopP,
                TopK = model.TopK,
                Tools = effectiveTools,
                Reasoning = new ReasoningOptions()
                {
                    Effort = (Microsoft.Extensions.AI.ReasoningEffort?)model.ReasoningEffort
                },
                ToolMode = effectiveTools == null ? null : ChatToolMode.Auto
            };
            return options;
        }

        private static IChatClient CreateLazyChatClient(AiProviderConfig provider, AiModelConfig model)
        {
            return new LazyChatClient(() => provider.Kind switch
            {
                AiProviderKind.Ollama => CreateOllamaChatClient(provider, model),
                AiProviderKind.OpenAi => CreateOpenAiChatClient(provider, model),
                AiProviderKind.OpenAiCompatible => CreateOpenAiChatClient(provider, model),
                AiProviderKind.Google => CreateGoogleChatClient(provider, model),
                AiProviderKind.Anthropic => CreateAnthropicChatClient(provider, model),
                _ => throw new InvalidOperationException("Selected AI provider is not supported.")
            });
        }

        private static IChatClient CreateOpenAiChatClient(AiProviderConfig provider, AiModelConfig model)
        {
            // Configuation
            ApiKeyCredential clientApiCredentials = new(ResolveApiKey(provider));
            OpenAIClientOptions clientOptions = new()
            {
                RetryPolicy = new ClientRetryPolicy(provider.MaxRetries),
                NetworkTimeout = TimeSpan.FromSeconds(provider.TimeoutSeconds)
            };
            if (!string.IsNullOrWhiteSpace(provider.EndpointUrl))
            {
                clientOptions.Endpoint = new Uri(provider.EndpointUrl);
            }
            // Create Client
            return new OpenAI.Chat.ChatClient(model.ModelId, clientApiCredentials, clientOptions).AsIChatClient();
        }

        private static IChatClient CreateOllamaChatClient(AiProviderConfig provider, AiModelConfig model)
        {
            // Configuation
            HttpClient httpClient = new()
            {
                BaseAddress = new Uri(provider.EndpointUrl),
                Timeout = TimeSpan.FromSeconds(provider.TimeoutSeconds)
            };
            // Create Client
            return new OllamaApiClient(httpClient, model.ModelId);
        }

        private static IChatClient CreateGoogleChatClient(AiProviderConfig provider, AiModelConfig model)
        {
            // Configuration
            string apiKey = ResolveApiKey(provider);
            Google.GenAI.Types.HttpOptions httpOptions = new()
            {
                Timeout = provider.TimeoutSeconds
            };
            if (!string.IsNullOrWhiteSpace(provider.EndpointUrl))
            {
                httpOptions.BaseUrl = provider.EndpointUrl;
            }
            // Create Client
            Google.GenAI.Client client = new(apiKey: apiKey, httpOptions: httpOptions);
            return client.AsIChatClient(model.ModelId);
        }

        private static IChatClient CreateAnthropicChatClient(AiProviderConfig provider, AiModelConfig model)
        {
            // Configuration
            ClientOptions clientOptions = new()
            {
                ApiKey = ResolveApiKey(provider),
                MaxRetries = provider.MaxRetries,
                Timeout = TimeSpan.FromSeconds(provider.TimeoutSeconds)
            };
            if (!string.IsNullOrWhiteSpace(provider.EndpointUrl))
            {
                clientOptions.BaseUrl = provider.EndpointUrl;
            }
            // Create Client
            AnthropicClient client = new(clientOptions);
            return client.AsIChatClient(model.ModelId);
        }

        private static string ResolveApiKey(AiProviderConfig provider)
        {
            if (string.IsNullOrWhiteSpace(provider.ApiKeyEnvVariable))
            {
                throw new InvalidOperationException("Provider API key environment variable is not configured.");
            }
            string? apiKey = Environment.GetEnvironmentVariable(provider.ApiKeyEnvVariable);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException($"Environment variable {provider.ApiKeyEnvVariable} is not set.");
            }
            return apiKey;
        }

        private sealed class LazyChatClient(Func<IChatClient> clientFactory) : IChatClient
        {
            private readonly Lazy<IChatClient> lazyClient = new(clientFactory, LazyThreadSafetyMode.ExecutionAndPublication);
            private bool disposed;

            private IChatClient Client
            {
                get
                {
                    ObjectDisposedException.ThrowIf(disposed, this);
                    return lazyClient.Value;
                }
            }

            /// <inheritdoc />
            public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            {
                return Client.GetResponseAsync(messages, options, cancellationToken);
            }

            /// <inheritdoc />
            public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (ChatResponseUpdate update in Client.GetStreamingResponseAsync(messages, options, cancellationToken).WithCancellation(cancellationToken))
                {
                    yield return update;
                }
            }

            /// <inheritdoc />
            public object? GetService(Type serviceType, object? serviceKey = null)
            {
                if (serviceType.IsInstanceOfType(this))
                {
                    return this;
                }
                return lazyClient.IsValueCreated ? Client.GetService(serviceType, serviceKey) : null;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                disposed = true;
                if (lazyClient.IsValueCreated)
                {
                    lazyClient.Value.Dispose();
                }
            }
        }
    }
}
