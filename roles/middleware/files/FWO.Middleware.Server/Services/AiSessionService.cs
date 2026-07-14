using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Ai;
using FWO.Logging;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Handles AI assistant session and message persistence.
    /// </summary>
    public class AiSessionService(ApiConnection apiConnection, AiSettingsService settingsService)
    {
        private readonly ApiConnection apiConnection = apiConnection;
        private readonly AiSettingsService settingsService = settingsService;

        /// <summary>
        /// Gets AI sessions visible to a user.
        /// </summary>
        public async Task<List<AiSession>> GetSessions(int userId)
        {
            return await apiConnection.SendQueryAsync<List<AiSession>>(AiQueries.getAiSessionsForUser, new { userId });
        }

        /// <summary>
        /// Gets a single AI session when the user may read it.
        /// </summary>
        public async Task<AiSession?> GetSession(long sessionId, int userId)
        {
            List<AiSession> sessions = await apiConnection.SendQueryAsync<List<AiSession>>(AiQueries.getAiSessionById, new { id = sessionId });
            AiSession? session = sessions.FirstOrDefault();
            return CanRead(session, userId) ? session : null;
        }

        /// <summary>
        /// Creates a new AI session for a user.
        /// </summary>
        /// <exception cref="InvalidOperationException">No enabled AI model is configured.</exception>
        public async Task<AiSession> CreateSession(int userId, string? name)
        {
            AiSettings settings = await settingsService.GetSettings();
            (AiProviderConfig Provider, AiModelConfig Model) selection = AiSettingsService.ResolveModel(settings)
                ?? throw new InvalidOperationException("No enabled AI model is configured.");
            string resolvedModel = selection.Model.ModelId;
            string sessionName = string.IsNullOrWhiteSpace(name) ? "New assistant session" : name.Trim();
            ReturnIdWrapper wrapper = await apiConnection.SendQueryAsync<ReturnIdWrapper>(AiQueries.addAiSession, new
            {
                userId,
                name = sessionName,
                systemPrompt = settings.SystemPrompt,
                modelId = resolvedModel
            });
            long sessionId = wrapper.ReturnIds?.FirstOrDefault()?.NewIdLong ?? 0;
            return await GetSession(sessionId, userId) ?? new AiSession
            {
                Id = sessionId,
                UserId = userId,
                Name = sessionName,
                ModelId = resolvedModel
            };
        }

        /// <summary>
        /// Renames an AI session when the user may mutate it.
        /// </summary>
        public async Task<bool> RenameSession(long sessionId, int userId, string name)
        {
            AiSession? session = await GetSession(sessionId, userId);
            if (!CanMutate(session, userId))
            {
                return false;
            }
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(AiQueries.renameAiSession, new { id = sessionId, name });
            return result.UpdatedIdLong == sessionId;
        }

        /// <summary>
        /// Deletes an AI session when the user may mutate it.
        /// </summary>
        public async Task<bool> DeleteSession(long sessionId, int userId)
        {
            AiSession? session = await GetSession(sessionId, userId);
            if (!CanMutate(session, userId))
            {
                return false;
            }
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(AiQueries.deleteAiSession, new { id = sessionId });
            Log.WriteAudit("AI Session", $"Deleted AI session {sessionId}.");
            return result.DeletedIdLong == sessionId;
        }

        /// <summary>
        /// Persists the serialized Microsoft Agent Framework session state for a session.
        /// Ownership is enforced in the mutation so a stale or forged session id cannot update another user's session state.
        /// </summary>
        public async Task<bool> SaveState(long sessionId, int userId, string state)
        {
            ReturnIdWrapper result = await apiConnection.SendQueryAsync<ReturnIdWrapper>(AiQueries.saveAiSessionState, new
            {
                id = sessionId,
                userId,
                state
            });
            return result.ReturnIds?.FirstOrDefault()?.UpdatedIdLong == sessionId;
        }

        private static bool CanRead(AiSession? session, int userId)
        {
            return session != null && (session.UserId == userId);
        }

        private static bool CanMutate(AiSession? session, int userId)
        {
            return session != null && session.UserId == userId;
        }
    }
}
