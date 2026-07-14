using FWO.Basics;
using FWO.Data.Ai;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// REST controller for AI assistant settings, sessions, and transcripts.
    /// Conversation runs are streamed by the native AG-UI endpoint (see Program.cs MapAGUI), not here.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AiController(AiSettingsService settingsService, AiSessionService sessionService,
        AiTranscriptService transcriptService) : ControllerBase
    {
        private readonly AiSettingsService settingsService = settingsService;
        private readonly AiSessionService sessionService = sessionService;
        private readonly AiTranscriptService transcriptService = transcriptService;

        /// <summary>
        /// Gets the global AI assistant settings.
        /// </summary>
        [HttpGet("Settings")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        public async Task<ActionResult<AiSettings>> GetSettings()
        {
            return Ok(await settingsService.GetSettings());
        }

        /// <summary>
        /// Gets the enabled AI models selectable on the assistant page.
        /// </summary>
        [HttpGet("Assistant/Settings")]
        public async Task<ActionResult<AiAssistantSettings>> GetAssistantSettings()
        {
            return Ok(await settingsService.GetAssistantSettings());
        }

        /// <summary>
        /// Saves the global AI assistant settings.
        /// </summary>
        [HttpPut("Settings")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<ActionResult<AiSettings>> SaveSettings([FromBody] AiSettings settings)
        {
            return Ok(await settingsService.SaveSettings(settings));
        }

        /// <summary>
        /// Tests provider connectivity without storing secrets.
        /// </summary>
        [HttpPost("Providers/Test")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<ActionResult<AiOperationResult>> TestProvider([FromBody] AiConnectionTestParameters parameters)
        {
            return Ok(await settingsService.TestProvider(parameters.Provider));
        }

        /// <summary>
        /// Gets AI sessions visible to the current user.
        /// </summary>
        [HttpGet("Sessions")]
        public async Task<ActionResult<List<AiSession>>> GetSessions()
        {
            if (!TryGetUserId(out int userId))
            {
                return Forbid();
            }
            return Ok(await sessionService.GetSessions(userId));
        }

        /// <summary>
        /// Gets one AI session when the current user may read it.
        /// </summary>
        [HttpGet("Sessions/{id:long}")]
        public async Task<ActionResult<AiSession>> GetSession(long id)
        {
            if (!TryGetUserId(out int userId))
            {
                return Forbid();
            }
            AiSession? session = await sessionService.GetSession(id, userId);
            return session == null ? Forbid() : Ok(session);
        }

        /// <summary>
        /// Creates an AI session for the current user. Returns a conflict with a translation key
        /// when no enabled AI model is configured, so no unusable session is persisted.
        /// </summary>
        [HttpPost("Sessions")]
        public async Task<ActionResult<AiSession>> CreateSession([FromBody] AiCreateSessionParameters parameters)
        {
            if (!TryGetUserId(out int userId))
            {
                return Forbid();
            }
            try
            {
                return Ok(await sessionService.CreateSession(userId, parameters.Name));
            }
            catch (InvalidOperationException)
            {
                return Conflict("ai_no_model_configured");
            }
        }

        /// <summary>
        /// Renames an AI session.
        /// </summary>
        [HttpPatch("Sessions/{id:long}")]
        public async Task<ActionResult<bool>> RenameSession(long id, [FromBody] AiUpdateSessionParameters parameters)
        {
            if (!TryGetUserId(out int userId))
            {
                return Forbid();
            }
            bool updated = await sessionService.RenameSession(id, userId, parameters.Name);
            return updated ? Ok(true) : Forbid();
        }

        /// <summary>
        /// Deletes an AI session.
        /// </summary>
        [HttpDelete("Sessions/{id:long}")]
        public async Task<ActionResult<bool>> DeleteSession(long id)
        {
            if (!TryGetUserId(out int userId))
            {
                return Forbid();
            }
            bool deleted = await sessionService.DeleteSession(id, userId);
            return deleted ? Ok(true) : Forbid();
        }

        /// <summary>
        /// Gets the user and assistant messages of a session for the assistant timeline.
        /// Conversation history is reconstructed from the persisted Agent Framework session state.
        /// </summary>
        [HttpGet("Sessions/{id:long}/Messages")]
        public async Task<ActionResult<List<AiChatMessage>>> GetSessionMessages(long id)
        {
            if (!TryGetUserId(out int userId))
            {
                return Forbid();
            }
            List<AiChatMessage>? messages = await transcriptService.GetMessages(id, userId, HttpContext.RequestAborted);
            return messages == null ? Forbid() : Ok(messages);
        }

        private bool TryGetUserId(out int userId)
        {
            List<int> userIds = JwtClaimParser.ExtractIntClaimValues(User.Claims, "x-hasura-user-id");
            userId = userIds.FirstOrDefault();
            return userIds.Count > 0;
        }
    }
}
