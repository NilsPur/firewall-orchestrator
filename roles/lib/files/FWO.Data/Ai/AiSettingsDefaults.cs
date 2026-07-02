namespace FWO.Data.Ai
{
    public static class AiSettingsDefaults
    {
        public static readonly Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettings = new()
        {
            ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace
        };

        public const string SystemPrompt = "You are the Firewall Orchestrator assistant. Answer using the user's FWO access scope and prefer read-only tools for factual FWO data.";
        public static readonly List<string> RecommendedOllamaModels =
        [
            "qwen3.5:2b",
            "qwen3.5:4b",
            "qwen3.5:9b",
            "qwen3.6:27b",
            "gemma4:12b",
            "gemma4:31b"
        ];

        public static List<AiProviderConfig> CreateProviders()
        {
            return
            [
                new()
                {
                    DisplayName = "Ollama",
                    Kind = AiProviderKind.Ollama,
                    EndpointUrl = "http://127.0.0.1:11434",
                    Enabled = true
                },
                new()
                {
                    DisplayName = "OpenAI",
                    Kind = AiProviderKind.OpenAi,
                    ApiKeyEnvVariable = "OPENAI_API_KEY"
                },
                new()
                {
                    DisplayName = "Anthropic",
                    Kind = AiProviderKind.Anthropic,
                    ApiKeyEnvVariable = "ANTHROPIC_API_KEY"
                },
                new()
                {
                    DisplayName = "Google",
                    Kind = AiProviderKind.Google,
                    ApiKeyEnvVariable = "GOOGLE_API_KEY"
                },
                new()
                {
                    DisplayName = "OpenAI-compatible",
                    Kind = AiProviderKind.OpenAiCompatible
                }
            ];
        }

    }
}
