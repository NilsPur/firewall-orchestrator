using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Compaction of the stored history of a session. Once the history exceeds the context window minus the reserve,
/// all of it but the most recent message groups is replaced by one summary written by the model, before the next
/// turn is sent. Nothing else is compacted or dropped.
/// </summary>
internal static class HistoryCompaction
{
    /// <summary>
    /// Number of most recent message groups (a user message, an answer, or a tool call with its results) that are
    /// never summarized.
    /// </summary>
    internal const int kPreservedRecentGroups = 4;

    /// <summary>
    /// Instruction for writing the summary: the default of Microsoft Agent Framework
    /// (SummarizationCompactionStrategy.DefaultSummarizationPrompt), plus keeping identifiers exactly as given,
    /// as later turns refer to firewall objects by them.
    /// </summary>
    internal const string kSummarizationPrompt =
        """
        You are a conversation summarizer. Produce a concise summary of the conversation that preserves:

        - Key facts, decisions, and user preferences
        - Important context needed for future turns
        - Tool call outcomes and their significance

        Keep names, UIDs, IP addresses, networks, ports and other identifiers when relevant, exactly as given.

        Omit pleasantries and redundant exchanges. Be factual and brief.
        """;

    /// <summary>
    /// Creates the reducer the history provider applies before the history is sent. It summarizes all older groups
    /// in one model call: with the default target it would summarize just enough to get below the limit, and then
    /// summarize again in nearly every turn. The summary is stored as a user message, see <see cref="SummaryAsUserMessageReducer"/>.
    /// </summary>
    /// <param name="summarizerClient">Chat client writing the summary; the plain provider client, without tools and
    /// without agent loop</param>
    /// <param name="contextWindowTokens">Context window of the model in tokens</param>
    /// <param name="reserveTokens">Tokens kept free of stored history, see <see cref="AgentSettings.CompactionReserveTokens"/></param>
    internal static IChatReducer CreateReducer(IChatClient summarizerClient, int contextWindowTokens, int reserveTokens)
    {
        ArgumentNullException.ThrowIfNull(summarizerClient);
        int compactionThresholdTokens = contextWindowTokens - reserveTokens;
        SummarizationCompactionStrategy summarization = new(
            summarizerClient,
            CompactionTriggers.TokensExceed(compactionThresholdTokens),
            minimumPreservedGroups: kPreservedRecentGroups,
            summarizationPrompt: kSummarizationPrompt,
            target: CompactionTriggers.Never);
        return new SummaryAsUserMessageReducer(summarization.AsChatReducer());
    }

    /// <summary>
    /// Stores the summary as a user message instead of the assistant message Microsoft Agent Framework writes.
    /// The summary is the first message of the stored history, as the instructions are not part of it, and the
    /// preserved groups after it may start with a tool call of the model. Providers expect a history to start with a
    /// user message, and Gemini rejects a tool call right after a model message. As a user message, the summary is
    /// summarized again with the older messages at the next compaction.
    /// </summary>
    private sealed class SummaryAsUserMessageReducer : IChatReducer
    {
        private readonly IChatReducer summarizingReducer;

        /// <summary>
        /// Wraps the reducer writing the summary.
        /// </summary>
        /// <param name="summarizingReducer">Reducer of the summarization strategy</param>
        internal SummaryAsUserMessageReducer(IChatReducer summarizingReducer)
        {
            this.summarizingReducer = summarizingReducer;
        }

        /// <summary>
        /// Compacts the history if needed and turns a new summary into a user message.
        /// </summary>
        /// <param name="messages">Stored history</param>
        /// <param name="cancellationToken">Cancels the summary request</param>
        public async Task<IEnumerable<ChatMessage>> ReduceAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            IEnumerable<ChatMessage> reducedMessages = await summarizingReducer.ReduceAsync(messages, cancellationToken);
            return reducedMessages.Select(ToUserMessageIfSummary).ToList();
        }

        /// <summary>
        /// Returns a copy of a summary written by the framework as a user message, and any other message unchanged.
        /// </summary>
        private static ChatMessage ToUserMessageIfSummary(ChatMessage message)
        {
            bool isSummary = message.Role == ChatRole.Assistant
                && message.AdditionalProperties?.ContainsKey(CompactionMessageGroup.SummaryPropertyKey) == true;
            if (!isSummary)
            {
                return message;
            }
            ChatMessage userMessage = message.Clone();
            userMessage.Role = ChatRole.User;
            return userMessage;
        }
    }
}
