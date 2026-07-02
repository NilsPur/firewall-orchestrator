using FWO.Data.Ai;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Projects the serialized Microsoft Agent Framework session of an AI session into a flat
    /// list of chat messages for display in the assistant timeline.
    /// </summary>
    public class AiTranscriptService(AiSessionService sessionService, AiSettingsService settingsService, AgentFactoryService agentFactory)
    {
        private readonly AiSessionService sessionService = sessionService;
        private readonly AiSettingsService settingsService = settingsService;
        private readonly AgentFactoryService agentFactory = agentFactory;

        /// <summary>
        /// Returns the user and assistant messages of a session, or null when the user may not read it.
        /// </summary>
        public async Task<List<AiChatMessage>?> GetMessages(long sessionId, int userId, CancellationToken cancellationToken)
        {
            AiSession? session = await sessionService.GetSession(sessionId, userId);
            if (session == null)
            {
                return null;
            }
            string? stateJson = ExtractState(session.State);
            if (stateJson == null)
            {
                return [];
            }

            AIAgent? agent = await ResolveAgent(session);
            if (agent == null)
            {
                return [];
            }
            using JsonDocument document = JsonDocument.Parse(stateJson);
            JsonElement normalizedState = AgentSessionJson.PrepareForDeserialize(document.RootElement);
            AgentSession agentSession = await agent.DeserializeSessionAsync(normalizedState, AgentSessionJson.Options, cancellationToken);
            List<ChatMessage> messages = agent.GetService<InMemoryChatHistoryProvider>()?.GetMessages(agentSession) ?? [];

            return messages
                .Where(message => message.Role == ChatRole.User || message.Role == ChatRole.Assistant)
                .Select(message => new AiChatMessage { Role = MapRole(message.Role), Content = message.Text })
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .ToList();
        }

        /// <summary>
        /// Resolves an agent that can deserialize the session. Session serialization is provider
        /// independent, so any enabled model works when the session's exact model is unavailable.
        /// </summary>
        private async Task<AIAgent?> ResolveAgent(AiSession session)
        {
            AiSettings settings = await settingsService.GetSettings();
            try
            {
                (AiProviderConfig provider, AiModelConfig model) = AgentFactoryService.ResolveProviderModel(settings, session.ProviderId, session.ModelId);
                return agentFactory.GetOrBuildAgent(provider, model);
            }
            catch (InvalidOperationException)
            {
                AiProviderConfig? fallback = settings.Providers.FirstOrDefault(provider => provider.Enabled && provider.Models.Any(model => model.Enabled));
                return fallback == null ? null : agentFactory.GetOrBuildAgent(fallback, fallback.Models.First(model => model.Enabled));
            }
        }

        private static string? ExtractState(JToken state)
        {
            if (state == null || state.Type == JTokenType.Null || (state is JObject jObject && !jObject.HasValues))
            {
                return null;
            }
            return state.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string MapRole(ChatRole role)
        {
            return role == ChatRole.User ? AiMessageRole.User.ToString() : AiMessageRole.AssistantOutput.ToString();
        }
    }
}
