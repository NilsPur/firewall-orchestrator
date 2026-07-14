using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data.Ai;
using FWO.Data.Middleware;
using FWO.Logging;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Handles global AI assistant settings stored in global config plus the single provider/model row.
    /// </summary>
    public class AiSettingsService(ApiConnection apiConnection)
    {
        private const string kAssistantActiveConfigKey = "aiAssistantActive";
        private readonly ApiConnection apiConnection = apiConnection;

        /// <summary>
        /// Reads the global AI settings, returning defaults when the settings cannot be read.
        /// </summary>
        public async Task<AiSettings> GetSettings()
        {
            try
            {
                List<ConfigItem> configItems = await apiConnection.SendQueryAsync<List<ConfigItem>>(AiQueries.getAiConfig);
                List<AiProviderConfig> providers = await apiConnection.SendQueryAsync<List<AiProviderConfig>>(AiQueries.getAiProviders);
                AiSettings settings = CreateSettings(providers);
                ApplyConfigSettings(settings, configItems);
                return settings;
            }
            catch (Exception exception)
            {
                Log.WriteError("AI settings", "Could not read AI settings. Falling back to defaults.", exception);
                return new AiSettings();
            }
        }

        /// <summary>
        /// Reads the assistant model setting available to every authenticated assistant user.
        /// </summary>
        public async Task<AiAssistantSettings> GetAssistantSettings()
        {
            return CreateAssistantSettings(await GetSettings());
        }

        /// <summary>
        /// Filters global AI settings down to model display data for the assistant page.
        /// </summary>
        public static AiAssistantSettings CreateAssistantSettings(AiSettings settings)
        {
            NormalizeSingleSettings(settings);
            return new AiAssistantSettings { Active = settings.Active, Model = settings.Model };
        }

        /// <summary>
        /// Saves the global AI settings. The provider and model are fixed to one row each.
        /// </summary>
        public async Task<AiSettings> SaveSettings(AiSettings settings)
        {
            NormalizeSingleSettings(settings);
            await apiConnection.SendQueryAsync<object>(AiQueries.saveAiSettings, new
            {
                configItems = new List<ConfigItem>
                {
                    new() { Key = kAssistantActiveConfigKey, Value = settings.Active.ToString(), User = 0 },
                    new() { Key = "system_prompt", Value = settings.SystemPrompt, User = 0 }
                },
                provider = BuildProviderInsert(settings.Provider),
                model = BuildModelInsert(settings.Model)
            });
            Log.WriteAudit("AI Settings", "Updated AI provider, model, or prompt settings.");
            return settings;
        }

        /// <summary>
        /// Tests whether a provider is configured and reachable.
        /// </summary>
        public async Task<AiOperationResult> TestProvider(AiProviderConfig provider)
        {
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
        /// Resolves the configured provider/model pair when the assistant is active.
        /// </summary>
        public static (AiProviderConfig Provider, AiModelConfig Model)? ResolveModel(AiSettings settings)
        {
            NormalizeSingleSettings(settings);
            return settings.Active && settings.Model.Enabled ? (settings.Provider, settings.Model) : null;
        }

        private static AiSettings CreateSettings(List<AiProviderConfig> providers)
        {
            AiProviderConfig provider = providers.FirstOrDefault() ?? AiSettingsDefaults.CreateProvider();
            AiModelConfig model = provider.Models.FirstOrDefault() ?? AiSettingsDefaults.CreateModel();
            AiSettings settings = new() { Provider = provider, Model = model };
            NormalizeSingleSettings(settings);
            return settings;
        }

        private static void NormalizeSingleSettings(AiSettings settings)
        {
            settings.Provider.Id = AiSettingsDefaults.ProviderId;
            settings.Provider.DisplayName = string.IsNullOrWhiteSpace(settings.Provider.DisplayName)
                ? AiSettingsDefaults.ProviderDisplayName
                : settings.Provider.DisplayName;
            settings.Model.ProviderId = settings.Provider.Id;
            settings.Model.ProviderDisplayName = settings.Provider.DisplayName;
            settings.Model.ProviderKind = settings.Provider.Kind;
            settings.Model.ModelId = string.IsNullOrWhiteSpace(settings.Model.ModelId) ? AiSettingsDefaults.ModelId : settings.Model.ModelId;
            settings.Model.DisplayName = string.IsNullOrWhiteSpace(settings.Model.DisplayName) ? settings.Model.ModelId : settings.Model.DisplayName;
            settings.Model.Enabled = true;
            settings.Provider.Models = [settings.Model];
        }

        private static void ApplyConfigSettings(AiSettings settings, List<ConfigItem> configItems)
        {
            ConfigItem? assistantActive = configItems.FirstOrDefault(item => item.Key == kAssistantActiveConfigKey);
            if (bool.TryParse(assistantActive?.Value, out bool active))
            {
                settings.Active = active;
            }

            ConfigItem? systemPrompt = configItems.FirstOrDefault(item => item.Key == "system_prompt");
            if (systemPrompt != null)
            {
                settings.SystemPrompt = systemPrompt.Value ?? "";
            }
        }

        private static object BuildProviderInsert(AiProviderConfig provider)
        {
            return new Dictionary<string, object?>
            {
                ["id"] = provider.Id,
                ["kind"] = provider.Kind.ToString(),
                ["display_name"] = provider.DisplayName,
                ["endpoint_url"] = provider.EndpointUrl,
                ["api_key_env_variable"] = provider.ApiKeyEnvVariable,
                ["timeout_seconds"] = provider.TimeoutSeconds,
                ["max_retries"] = provider.MaxRetries
            };
        }

        private static object BuildModelInsert(AiModelConfig model)
        {
            return new
            {
                model_id = model.ModelId,
                provider_id = model.ProviderId,
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
