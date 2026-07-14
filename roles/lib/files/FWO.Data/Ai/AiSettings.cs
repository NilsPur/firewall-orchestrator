using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Ai
{
    public class AiSettings
    {
        [JsonProperty("system_prompt"), JsonPropertyName("system_prompt")]
        public string SystemPrompt { get; set; } = AiSettingsDefaults.SystemPrompt;

        [JsonProperty("provider"), JsonPropertyName("provider")]
        public AiProviderConfig Provider { get; set; } = AiSettingsDefaults.CreateProvider();

        [JsonProperty("model"), JsonPropertyName("model")]
        public AiModelConfig Model { get; set; } = AiSettingsDefaults.CreateModel();
    }

    public class AiAssistantSettings
    {
        [JsonProperty("model"), JsonPropertyName("model")]
        public AiModelConfig Model { get; set; } = AiSettingsDefaults.CreateModel();
    }

    public class AiProviderConfig
    {
        /// <summary>
        /// Stable provider id. Zero marks a provider added in the UI that has not been persisted yet;
        /// the database assigns its id on save. Sessions pin this id so several providers of the same
        /// kind can be distinguished.
        /// </summary>
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("display_name"), JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("kind"), JsonPropertyName("kind")]
        public AiProviderKind Kind { get; set; } = AiProviderKind.OpenAi;

        [JsonProperty("endpoint_url"), JsonPropertyName("endpoint_url")]
        public string EndpointUrl { get; set; } = "";

        [JsonProperty("api_key_env_variable"), JsonPropertyName("api_key_env_variable")]
        public string ApiKeyEnvVariable { get; set; } = "";

        [JsonProperty("timeout_seconds"), JsonPropertyName("timeout_seconds")]
        public int TimeoutSeconds { get; set; } = 60;

        [JsonProperty("max_retries"), JsonPropertyName("max_retries")]
        public int MaxRetries { get; set; } = 1;

        [JsonProperty("enabled"), JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonProperty("models"), JsonPropertyName("models")]
        public List<AiModelConfig> Models { get; set; } = [];
    }

    public enum ReasoningEffort
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        VeryHigh = 4
    }

    public class AiModelConfig
    {
        [JsonProperty("display_name"), JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("model_id"), JsonPropertyName("model_id")]
        public string ModelId { get; set; } = "";

        [JsonProperty("provider_id"), JsonPropertyName("provider_id")]
        public long ProviderId { get; set; }

        [JsonProperty("provider_display_name"), JsonPropertyName("provider_display_name")]
        public string ProviderDisplayName { get; set; } = "";

        [JsonProperty("provider_kind"), JsonPropertyName("provider_kind")]
        public AiProviderKind ProviderKind { get; set; } = AiProviderKind.OpenAi;

        [JsonProperty("streaming_supported"), JsonPropertyName("streaming_supported")]
        public bool? StreamingSupported { get; set; }

        [JsonProperty("tool_calls_supported"), JsonPropertyName("tool_calls_supported")]
        public bool? ToolCallsSupported { get; set; }

        [JsonProperty("vision_supported"), JsonPropertyName("vision_supported")]
        public bool? VisionSupported { get; set; }

        [JsonProperty("reasoning_supported"), JsonPropertyName("reasoning_supported")]
        public bool? ReasoningSupported { get; set; }

        [JsonProperty("context_size"), JsonPropertyName("context_size")]
        public int? ContextSize { get; set; }

        [JsonProperty("temperature"), JsonPropertyName("temperature")]
        public float? Temperature { get; set; }

        [JsonProperty("top_p"), JsonPropertyName("top_p")]
        public float? TopP { get; set; }

        [JsonProperty("top_k"), JsonPropertyName("top_k")]
        public int? TopK { get; set; }

        [JsonProperty("max_output_tokens"), JsonPropertyName("max_output_tokens")]
        public int? MaxOutputTokens { get; set; }

        [JsonProperty("reasoning_effort"), JsonPropertyName("reasoning_effort")]
        public ReasoningEffort? ReasoningEffort { get; set; }

        [JsonProperty("enabled"), JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;
    }
}
