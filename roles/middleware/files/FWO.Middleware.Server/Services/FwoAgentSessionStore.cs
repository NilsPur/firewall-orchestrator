using FWO.Basics;
using FWO.Data.Ai;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Persists Microsoft Agent Framework sessions for the AG-UI endpoint in the ai_session table.
    /// The AG-UI thread id is the ai_session id; every access is scoped to the requesting user
    /// (resolved from the JWT) so a caller can only resume their own sessions.
    /// </summary>
    public sealed class FwoAgentSessionStore(AiSessionService sessionService, IHttpContextAccessor httpContextAccessor) : AgentSessionStore
    {
        private readonly AiSessionService sessionService = sessionService;
        private readonly IHttpContextAccessor httpContextAccessor = httpContextAccessor;

        /// <inheritdoc/>
        public override async ValueTask<AgentSession> GetSessionAsync(AIAgent agent, string conversationId, CancellationToken cancellationToken = default)
        {
            (long sessionId, int userId) = ResolveAccess(conversationId);
            AiSession session = await sessionService.GetSession(sessionId, userId)
                ?? throw new UnauthorizedAccessException("AI session does not exist or is not owned by the current user.");

            AgentSession agentSession;
            if (TryGetState(session.State, out string stateJson))
            {
                using JsonDocument document = JsonDocument.Parse(stateJson);
                JsonElement normalizedState = AgentSessionJson.PrepareForDeserialize(document.RootElement);
                agentSession = await agent.DeserializeSessionAsync(normalizedState, AgentSessionJson.Options, cancellationToken);
            }
            else
            {
                agentSession = await agent.CreateSessionAsync(cancellationToken);
            }
            FwoRoutingAgent.StampModel(agentSession, session.ProviderId, session.ModelId);
            FwoRoutingAgent.StampSystemPrompt(agentSession, session.SystemPrompt);
            return agentSession;
        }

        /// <inheritdoc/>
        public override async ValueTask SaveSessionAsync(AIAgent agent, string conversationId, AgentSession session, CancellationToken cancellationToken = default)
        {
            (long sessionId, int userId) = ResolveAccess(conversationId);
            JsonElement state = await agent.SerializeSessionAsync(session, AgentSessionJson.Options, cancellationToken);
            JsonElement persistedState = AgentSessionJson.PrepareForPersist(state);
            await sessionService.SaveState(sessionId, userId, JToken.Parse(persistedState.GetRawText()));
        }

        private (long SessionId, int UserId) ResolveAccess(string conversationId)
        {
            if (!long.TryParse(conversationId, out long sessionId))
            {
                throw new UnauthorizedAccessException("AG-UI thread id is not a valid AI session id.");
            }
            int userId = ResolveUserId();
            if (userId <= 0)
            {
                throw new UnauthorizedAccessException("No authenticated user for the AI session request.");
            }
            return (sessionId, userId);
        }

        private int ResolveUserId()
        {
            System.Security.Claims.ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
            if (user == null)
            {
                return 0;
            }
            return JwtClaimParser.ExtractIntClaimValues(user.Claims, "x-hasura-user-id").FirstOrDefault();
        }

        private static bool TryGetState(JToken state, out string stateJson)
        {
            stateJson = "";
            if (state == null || state.Type == JTokenType.Null || (state is JObject jObject && !jObject.HasValues))
            {
                return false;
            }
            stateJson = state.ToString(Newtonsoft.Json.Formatting.None);
            return true;
        }
    }
}
