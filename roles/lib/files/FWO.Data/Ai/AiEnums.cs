namespace FWO.Data.Ai
{
    public enum AiProviderKind
    {
        Ollama,
        OpenAi,
        Anthropic,
        Google,
        OpenAiCompatible
    }

    public enum AiMessageRole
    {
        System,
        User,
        AssistantOutput,
        AssistantReasoning,
        Tool,
        Error
    }
}
