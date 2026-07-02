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

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Builds and caches Microsoft Agent Framework agents from persisted FWO AI settings.
    /// One agent is cached per provider/model configuration so that the streaming endpoint and
    /// the session store share the same provider clients instead of rebuilding them per request.
    /// </summary>
    public class AgentFactoryService(AiToolExecutionService toolExecutionService)
    {
        private readonly AiToolExecutionService toolExecutionService = toolExecutionService;
        private readonly ConcurrentDictionary<string, AIAgent> agentCache = new();

        /// <summary>
        /// Resolves the provider and model for a session's fixed selection against current settings.
        /// The provider is matched by its stable id so several providers of the same kind stay distinct.
        /// </summary>
        /// <exception cref="InvalidOperationException">The provider or model is missing or disabled.</exception>
        public static (AiProviderConfig Provider, AiModelConfig Model) ResolveProviderModel(AiSettings settings, long providerId, string modelId)
        {
            AiProviderConfig provider = settings.Providers.FirstOrDefault(currentProvider => currentProvider.Id == providerId && currentProvider.Enabled)
                ?? throw new InvalidOperationException($"AI provider '{providerId}' is not configured or is disabled.");
            AiModelConfig model = provider.Models.FirstOrDefault(currentModel => currentModel.ModelId == modelId && currentModel.Enabled)
                ?? throw new InvalidOperationException($"AI model '{modelId}' is not configured or is disabled.");
            return (provider, model);
        }

        /// <summary>
        /// Returns a cached agent for the given provider and model, building it on first use.
        /// </summary>
        public AIAgent GetOrBuildAgent(AiProviderConfig provider, AiModelConfig model)
        {
            return agentCache.GetOrAdd(CacheKey(provider, model), _ => BuildAgent(provider, model, toolExecutionService.CreateTools()));
        }

        /// <summary>
        /// Builds a fresh agent for the given provider and model without caching.
        /// </summary>
        public AIAgent BuildAgent(AiProviderConfig provider, AiModelConfig model, IList<AITool>? tools)
        {
            ChatClientAgentOptions options = new()
            {
                ChatOptions = BuildChatOptions(model, tools)
            };

            // Create chat client with provider endpoint, retries, timeout and model specified.
            IChatClient chatClient = provider.Kind switch
            {
                AiProviderKind.Ollama => CreateOllamaChatClient(provider, model),
                AiProviderKind.OpenAi => CreateOpenAiChatClient(provider, model),
                AiProviderKind.OpenAiCompatible => CreateOpenAiChatClient(provider, model),
                AiProviderKind.Google => CreateGoogleChatClient(provider, model),
                AiProviderKind.Anthropic => CreateAnthropicChatClient(provider, model),
                _ => throw new InvalidOperationException("Selected AI provider is not supported.")
            };

            // Create ai agent with max output tokens, tools, temperature, top k / p, reasoning
            return chatClient.AsAIAgent(options);
        }

        /// <summary>
        /// Runs a single non-streaming prompt to verify a provider/model can be selected, returning the reply text.
        /// </summary>
        public async Task<string> RunTest(AiProviderConfig provider, AiModelConfig model, string prompt, CancellationToken cancellationToken)
        {
            AIAgent agent = BuildAgent(provider, model, null);
            AgentSession session = await agent.CreateSessionAsync(cancellationToken);
            AgentResponse response = await agent.RunAsync([new ChatMessage(ChatRole.User, prompt)], session, new AgentRunOptions(), cancellationToken);
            return response.Text;
        }

        /// <summary>
        /// Cache key that changes whenever a generation-relevant model setting changes, so edited
        /// configurations rebuild their agent instead of reusing a stale one.
        /// </summary>
        private static string CacheKey(AiProviderConfig provider, AiModelConfig model)
        {
            return string.Join("|", provider.Id, provider.Kind, model.ModelId, provider.EndpointUrl, provider.ApiKeyEnvVariable,
                model.Temperature, model.TopP, model.TopK, model.MaxOutputTokens, model.ReasoningEffort, provider.TimeoutSeconds,
                provider.MaxRetries, model.ToolCallsSupported);
        }

        internal static ChatOptions BuildChatOptions(AiModelConfig model, IList<AITool>? tools)
        {
            // Tool calling is opt-in because many local Ollama models reject chat requests that include tools.
            IList<AITool>? effectiveTools = model.ToolCallsSupported == true ? tools : null;
            ChatOptions options = new()
            {
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
    }
}
