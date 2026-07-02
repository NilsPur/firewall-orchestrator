using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data.Ai;
using FWO.Data.Middleware;
using FWO.Logging;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Handles global AI assistant settings stored in the global config, ai_provider, and ai_model
    /// tables. Providers keep stable ids across saves so sessions can pin a specific provider.
    /// </summary>
    public class AiSettingsService(ApiConnection apiConnection)
    {
        private readonly ApiConnection apiConnection = apiConnection;

        /// <summary>
        /// Reads the global AI settings, assembling global config values with the relational
        /// provider and model catalog. Returns defaults when the settings cannot be read.
        /// </summary>
        public async Task<AiSettings> GetSettings()
        {
            try
            {
                List<ConfigItem> configItems = await apiConnection.SendQueryAsync<List<ConfigItem>>(AiQueries.getAiConfig);
                List<AiProviderConfig> providers = await apiConnection.SendQueryAsync<List<AiProviderConfig>>(AiQueries.getAiProviders);
                AiSettings settings = new() { Providers = providers };
                ApplyConfigSettings(settings, configItems);
                StampProviderKindOntoModels(settings);
                return settings;
            }
            catch (Exception exception)
            {
                Log.WriteError("AI settings", "Could not read AI settings. Falling back to defaults.", exception);
                return new AiSettings();
            }
        }

        /// <summary>
        /// Reads the assistant model selection settings available to every authenticated assistant user.
        /// </summary>
        public async Task<AiAssistantSettings> GetAssistantSettings()
        {
            return CreateAssistantSettings(await GetSettings());
        }

        /// <summary>
        /// Filters global AI settings down to model selection data for the assistant page.
        /// </summary>
        public static AiAssistantSettings CreateAssistantSettings(AiSettings settings)
        {
            StampProviderKindOntoModels(settings);
            List<AiModelConfig> enabledModels = settings.Providers
                .Where(provider => provider.Enabled)
                .SelectMany(provider => provider.Models)
                .Where(model => model.Enabled)
                .ToList();

            string initialModelId = FindEnabledModelBySelection(settings, settings.InitialModelId)?.Model.SelectionId ?? "";

            return new AiAssistantSettings
            {
                InitialModelId = initialModelId,
                EnabledModels = enabledModels
            };
        }

        /// <summary>
        /// Saves the global AI settings. Prompt and model selection are upserted into global config,
        /// removed providers are deleted, and the remaining providers are upserted by id so existing
        /// provider ids are kept. Models are rewritten because nothing references a model by database id.
        /// </summary>
        public async Task<AiSettings> SaveSettings(AiSettings settings)
        {
            List<long> keepProviderIds = [.. settings.Providers.Where(provider => provider.Id > 0).Select(provider => provider.Id)];
            List<object> providers = [.. settings.Providers.Select(BuildProviderInsert)];

            await apiConnection.SendQueryAsync<object>(AiQueries.saveAiSettings, new
            {
                configItems = new List<ConfigItem>
                {
                    new() { Key = "system_prompt", Value = settings.SystemPrompt, User = 0 },
                    new() { Key = "aiLastModelId", Value = settings.InitialModelId, User = 0 }
                },
                keepProviderIds,
                providers
            });
            Log.WriteAudit("AI Settings", "Updated AI provider, model, or prompt settings.");
            return settings;
        }

        /// <summary>
        /// Tests whether a provider is configured and reachable.
        /// </summary>
        public async Task<AiOperationResult> TestProvider(AiProviderConfig provider)
        {
            if (!provider.Enabled)
            {
                return new AiOperationResult { Success = false, MessageKey = "ai_provider_disabled" };
            }
            if (!string.IsNullOrWhiteSpace(provider.ApiKeyEnvVariable) && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(provider.ApiKeyEnvVariable)))
            {
                return new AiOperationResult { Success = false, MessageKey = "ai_env_var_missing", MessageArgument = provider.ApiKeyEnvVariable };
            }
            if (string.IsNullOrWhiteSpace(provider.EndpointUrl))
            {
                return new AiOperationResult { Success = true, MessageKey = "ai_provider_valid" };
            }
            return await TestEndpoint(provider);
        }

        /// <summary>
        /// Resolves a provider-aware model selection against enabled settings, falling back to the
        /// configured initial model or the first enabled model when the request is unavailable.
        /// </summary>
        public static (AiProviderConfig Provider, AiModelConfig Model)? ResolveModelSelection(AiSettings settings, long providerId, string? modelId)
        {
            StampProviderKindOntoModels(settings);
            (AiProviderConfig Provider, AiModelConfig Model)? requestedSelection = FindEnabledModel(settings, providerId, modelId);
            if (requestedSelection != null)
            {
                return requestedSelection;
            }
            (AiProviderConfig Provider, AiModelConfig Model)? initialSelection = FindEnabledModelBySelection(settings, settings.InitialModelId);
            if (initialSelection != null)
            {
                return initialSelection;
            }
            foreach (AiProviderConfig provider in settings.Providers.Where(provider => provider.Enabled))
            {
                AiModelConfig? model = provider.Models.FirstOrDefault(model => model.Enabled);
                if (model != null)
                {
                    return (provider, model);
                }
            }
            return null;
        }

        private static void StampProviderKindOntoModels(AiSettings settings)
        {
            foreach (AiProviderConfig provider in settings.Providers)
            {
                foreach (AiModelConfig model in provider.Models)
                {
                    model.ProviderId = provider.Id;
                    model.ProviderDisplayName = provider.DisplayName;
                    model.ProviderKind = provider.Kind;
                }
            }
        }

        /// <summary>
        /// Applies AI prompt and global model selection config rows to the settings DTO.
        /// </summary>
        private static void ApplyConfigSettings(AiSettings settings, List<ConfigItem> configItems)
        {
            ConfigItem? systemPrompt = configItems.FirstOrDefault(item => item.Key == "system_prompt");
            if (systemPrompt != null)
            {
                settings.SystemPrompt = systemPrompt.Value ?? "";
            }

            ConfigItem? aiLastModelId = configItems.FirstOrDefault(item => item.Key == "aiLastModelId");
            if (aiLastModelId != null)
            {
                settings.InitialModelId = aiLastModelId.Value ?? "";
            }
        }

        private static object BuildProviderInsert(AiProviderConfig provider)
        {
            Dictionary<string, object?> insert = new()
            {
                ["kind"] = provider.Kind.ToString(),
                ["display_name"] = provider.DisplayName,
                ["endpoint_url"] = provider.EndpointUrl,
                ["api_key_env_variable"] = provider.ApiKeyEnvVariable,
                ["timeout_seconds"] = provider.TimeoutSeconds,
                ["max_retries"] = provider.MaxRetries,
                ["enabled"] = provider.Enabled,
                ["models"] = new { data = provider.Models.Select(BuildModelInsert).ToList() }
            };
            // Only persisted providers carry an id; new providers get one from the database sequence.
            if (provider.Id > 0)
            {
                insert["id"] = provider.Id;
            }
            return insert;
        }

        private static object BuildModelInsert(AiModelConfig model)
        {
            return new
            {
                model_id = model.ModelId,
                display_name = model.DisplayName,
                enabled = model.Enabled,
                streaming_supported = model.StreamingSupported,
                tool_calls_supported = model.ToolCallsSupported,
                vision_supported = model.VisionSupported,
                reasoning_supported = model.ReasoningSupported,
                context_size = model.ContextSize,
                temperature = model.Temperature,
                top_p = model.TopP,
                top_k = model.TopK,
                max_output_tokens = model.MaxOutputTokens,
                reasoning_effort = (int?)model.ReasoningEffort
            };
        }

        private static (AiProviderConfig Provider, AiModelConfig Model)? FindEnabledModelBySelection(AiSettings settings, string? selectionId)
        {
            if (AiModelConfig.TryParseSelectionId(selectionId, out long providerId, out string modelId))
            {
                return FindEnabledModel(settings, providerId, modelId);
            }
            foreach (AiProviderConfig provider in settings.Providers.Where(provider => provider.Enabled))
            {
                AiModelConfig? model = provider.Models.FirstOrDefault(model => model.Enabled && model.ModelId == selectionId);
                if (model != null)
                {
                    return (provider, model);
                }
            }
            return null;
        }

        private static (AiProviderConfig Provider, AiModelConfig Model)? FindEnabledModel(AiSettings settings, long providerId, string? modelId)
        {
            if (providerId <= 0 || string.IsNullOrWhiteSpace(modelId))
            {
                return null;
            }
            AiProviderConfig? provider = settings.Providers.FirstOrDefault(currentProvider => currentProvider.Id == providerId && currentProvider.Enabled);
            AiModelConfig? model = provider?.Models.FirstOrDefault(currentModel => currentModel.Enabled && currentModel.ModelId == modelId);
            return provider != null && model != null ? (provider, model) : null;
        }

        private static async Task<AiOperationResult> TestEndpoint(AiProviderConfig provider)
        {
            try
            {
                using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(Math.Max(1, provider.TimeoutSeconds)) };
                using HttpRequestMessage request = new(HttpMethod.Get, provider.EndpointUrl);
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                return new AiOperationResult
                {
                    Success = response.IsSuccessStatusCode,
                    MessageKey = "ai_endpoint_returned",
                    MessageArgument = ((int)response.StatusCode).ToString()
                };
            }
            catch (Exception exception)
            {
                return new AiOperationResult { Success = false, MessageKey = "ai_endpoint_unreachable", MessageArgument = exception.GetBaseException().Message };
            }
        }
    }
}
