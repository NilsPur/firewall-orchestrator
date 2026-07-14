namespace FWO.Data.Ai
{
    public static class AiSettingsDefaults
    {
        public const long ProviderId = 1;
        public const string ProviderDisplayName = "OpenAI";
        public const string ApiKeyEnvVariable = "OPENAI_API_KEY";
        public const string ModelId = "gpt-5.4-mini";
        public const string ModelDisplayName = "GPT-5.4 mini";

        public static readonly Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettings = new()
        {
            ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace
        };

        public const string SystemPrompt = "You are the Firewall Orchestrator assistant. Answer using the user's FWO access scope. Ground factual answers in data returned by FWO tools instead of assumptions. Use the appropriate read-only tool before answering factual FWO questions, and state when tool data is missing or insufficient.";

        public static AiProviderConfig CreateProvider()
        {
            return new()
            {
                Id = ProviderId,
                DisplayName = ProviderDisplayName,
                Kind = AiProviderKind.OpenAi,
                ApiKeyEnvVariable = ApiKeyEnvVariable
            };
        }

        public static AiModelConfig CreateModel()
        {
            return new()
            {
                DisplayName = ModelDisplayName,
                ModelId = ModelId,
                ProviderId = ProviderId,
                ProviderDisplayName = ProviderDisplayName,
                ProviderKind = AiProviderKind.OpenAi,
                Enabled = true,
                StreamingSupported = true,
                ToolCallsSupported = true,
                VisionSupported = false,
                ReasoningSupported = true,
                ContextSize = 400000,
                MaxOutputTokens = 128000,
                ReasoningEffort = ReasoningEffort.Medium
            };
        }
    }
}
