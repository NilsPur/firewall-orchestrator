using FWO.Data.Ai;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Single agent registered for the AG-UI endpoint that routes each run to the provider/model
    /// fixed on its session. The provider/model is stamped into the session state by
    /// <see cref="FwoAgentSessionStore"/> and read back here; the underlying provider agents are
    /// built and cached by <see cref="AgentFactoryService"/>.
    /// </summary>
    public sealed class FwoRoutingAgent(AgentFactoryService agentFactory, AiSettingsService settingsService) : AIAgent
    {
        /// <summary>
        /// Dependency-injection key and AG-UI agent name for the FWO assistant agent.
        /// </summary>
        public const string AgentName = "fwo-assistant";

        private const string kStateKey = "fwoSessionModel";

        private readonly AgentFactoryService agentFactory = agentFactory;
        private readonly AiSettingsService settingsService = settingsService;
        private static readonly ConditionalWeakTable<AgentSession, FwoSessionPrompt> sessionPrompts = new();

        /// <inheritdoc/>
        public override string? Name => AgentName;

        /// <inheritdoc/>
        public override string? Description => "Firewall Orchestrator assistant.";

        /// <summary>
        /// Records the provider/model selection on a session so later runs route to the right provider.
        /// </summary>
        public static void StampModel(AgentSession session, long providerId, string modelId)
        {
            session.StateBag.SetValue(kStateKey, new FwoSessionModel { ProviderId = providerId, ModelId = modelId });
        }

        /// <summary>
        /// Records a session's configured prompt for the current in-memory run only.
        /// </summary>
        public static void StampSystemPrompt(AgentSession session, string systemPrompt)
        {
            sessionPrompts.Remove(session);
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                sessionPrompts.Add(session, new FwoSessionPrompt { SystemPrompt = systemPrompt });
            }
        }

        /// <inheritdoc/>
        protected override async Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null,
            AgentRunOptions? options = null, CancellationToken cancellationToken = default)
        {
            AIAgent inner;
            try
            {
                inner = await ResolveAgent(session);
            }
            catch (InvalidOperationException exception)
            {
                return new AgentResponse(new ChatMessage(ChatRole.Assistant, [new ErrorContent(exception.Message)]));
            }
            List<ChatMessage> messagesWithPrompt = await BuildMessagesWithSystemPrompt(messages, session);
            return await inner.RunAsync(messagesWithPrompt, session, options, cancellationToken);
        }

        /// <inheritdoc/>
        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages,
            AgentSession? session = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            AIAgent? inner = null;
            ErrorContent? resolveError = null;
            try
            {
                inner = await ResolveAgent(session);
            }
            catch (InvalidOperationException exception)
            {
                resolveError = new ErrorContent(exception.Message);
            }
            if (inner == null)
            {
                yield return new AgentResponseUpdate(ChatRole.Assistant, [resolveError!]);
                yield break;
            }
            List<ChatMessage> messagesWithPrompt = await BuildMessagesWithSystemPrompt(messages, session);
            await foreach (AgentResponseUpdate update in inner.RunStreamingAsync(messagesWithPrompt, session, options, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
        }

        /// <inheritdoc/>
        protected override async ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        {
            AIAgent inner = await ResolveAgent(null);
            return await inner.CreateSessionAsync(cancellationToken);
        }

        /// <inheritdoc/>
        protected override async ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
        {
            AIAgent inner = await ResolveAgent(session);
            JsonElement state = await inner.SerializeSessionAsync(session, jsonSerializerOptions ?? AgentSessionJson.Options, cancellationToken);
            return AgentSessionJson.PrepareForPersist(state);
        }

        /// <inheritdoc/>
        protected override async ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
        {
            AIAgent inner = await ResolveAgent(null);
            JsonElement normalizedState = AgentSessionJson.PrepareForDeserialize(serializedState);
            return await inner.DeserializeSessionAsync(normalizedState, jsonSerializerOptions ?? AgentSessionJson.Options, cancellationToken);
        }

        /// <summary>
        /// Resolves the cached provider agent for a session's stamped model, falling back to the
        /// first enabled model when no selection is recorded yet (e.g. ephemeral session lifecycle)
        /// or when the stamped selection is no longer available.
        /// </summary>
        /// <exception cref="InvalidOperationException">No enabled AI model is configured.</exception>
        private async Task<AIAgent> ResolveAgent(AgentSession? session)
        {
            AiSettings settings = await settingsService.GetSettings();
            FwoSessionModel? stamped = session?.StateBag.GetValue<FwoSessionModel>(kStateKey);
            if (stamped != null && stamped.ProviderId > 0)
            {
                try
                {
                    (AiProviderConfig provider, AiModelConfig model) = AgentFactoryService.ResolveProviderModel(settings, stamped.ProviderId, stamped.ModelId);
                    return agentFactory.GetOrBuildAgent(provider, model);
                }
                catch (InvalidOperationException)
                {
                    // The session's pinned provider/model was removed or disabled; fall back to the default model.
                }
            }
            AiProviderConfig defaultProvider = settings.Providers.FirstOrDefault(provider => provider.Enabled && provider.Models.Any(model => model.Enabled))
                ?? throw new InvalidOperationException("No enabled AI model is configured.");
            return agentFactory.GetOrBuildAgent(defaultProvider, defaultProvider.Models.First(model => model.Enabled));
        }

        private async Task<List<ChatMessage>> BuildMessagesWithSystemPrompt(IEnumerable<ChatMessage> messages, AgentSession? session)
        {
            string systemPrompt = await ResolveSystemPrompt(session);
            return PrependSystemPrompt(messages, systemPrompt);
        }

        private async Task<string> ResolveSystemPrompt(AgentSession? session)
        {
            if (session != null && sessionPrompts.TryGetValue(session, out FwoSessionPrompt? prompt) && !string.IsNullOrWhiteSpace(prompt.SystemPrompt))
            {
                return prompt.SystemPrompt;
            }
            AiSettings settings = await settingsService.GetSettings();
            return settings.SystemPrompt;
        }

        /// <summary>
        /// Adds the configured prompt as a system message for the provider call.
        /// </summary>
        internal static List<ChatMessage> PrependSystemPrompt(IEnumerable<ChatMessage> messages, string systemPrompt)
        {
            List<ChatMessage> messageList = messages.ToList();
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                return messageList;
            }
            if (messageList.FirstOrDefault() is { Role: var role, Text: var text }
                && role == ChatRole.System
                && string.Equals(text, systemPrompt, StringComparison.Ordinal))
            {
                return messageList;
            }
            return [new ChatMessage(ChatRole.System, systemPrompt), .. messageList];
        }

        /// <summary>
        /// Provider/model selection persisted in a session's state bag.
        /// </summary>
        internal sealed class FwoSessionModel
        {
            public long ProviderId { get; set; }
            public string ModelId { get; set; } = "";
        }

        private sealed class FwoSessionPrompt
        {
            public string SystemPrompt { get; set; } = "";
        }
    }
}
