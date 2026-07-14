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
    public class AiTranscriptService(AiSessionService sessionService, AgentFactoryService agentFactory)
    {
        private readonly AiSessionService sessionService = sessionService;
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

            AIAgent agent;
            try
            {
                agent = await agentFactory.GetAgent();
            }
            catch (InvalidOperationException)
            {
                return [];
            }
            using JsonDocument document = JsonDocument.Parse(stateJson);
            AgentSession agentSession = await agent.DeserializeSessionAsync(document.RootElement, AgentSessionJson.Options, cancellationToken);
            List<ChatMessage> messages = agent.GetService<InMemoryChatHistoryProvider>()?.GetMessages(agentSession) ?? [];

            return messages
                .Where(message => message.Role == ChatRole.User || message.Role == ChatRole.Assistant)
                .Select(message => new AiChatMessage { Role = MapRole(message.Role), Content = message.Text })
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .ToList();
        }

        private static string? ExtractState(string state)
        {
            return string.IsNullOrWhiteSpace(state) ? null : state;
        }

        private static string MapRole(ChatRole role)
        {
            return role == ChatRole.User ? AiMessageRole.User.ToString() : AiMessageRole.AssistantOutput.ToString();
        }
    }
}
