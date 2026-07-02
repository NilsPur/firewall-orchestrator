using FWO.Data.Ai;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Middleware
{
    public class AiCreateSessionParameters
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("provider_id"), JsonPropertyName("provider_id")]
        public long ProviderId { get; set; }

        [JsonProperty("model_id"), JsonPropertyName("model_id")]
        public string? ModelId { get; set; }
    }

    public class AiUpdateSessionParameters
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    public class AiConnectionTestParameters
    {
        [JsonProperty("provider"), JsonPropertyName("provider")]
        public AiProviderConfig Provider { get; set; } = new();
    }

    public class AiModelTestParameters
    {
        [JsonProperty("provider"), JsonPropertyName("provider")]
        public AiProviderConfig Provider { get; set; } = new();

        [JsonProperty("model"), JsonPropertyName("model")]
        public AiModelConfig Model { get; set; } = new();

        [JsonProperty("sample_prompt"), JsonPropertyName("sample_prompt")]
        public string SamplePrompt { get; set; } = "Reply with OK.";
    }

    public class AiOperationResult
    {
        [JsonProperty("success"), JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonProperty("message_key"), JsonPropertyName("message_key")]
        public string MessageKey { get; set; } = "";

        [JsonProperty("message_argument"), JsonPropertyName("message_argument")]
        public string MessageArgument { get; set; } = "";
    }

    public class AiOllamaModelParameters
    {
        [JsonProperty("model_id"), JsonPropertyName("model_id")]
        public string ModelId { get; set; } = "";
    }
}
