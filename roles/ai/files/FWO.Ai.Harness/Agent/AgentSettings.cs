namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Settings of the agents created by an <see cref="FwoAgentFactory"/>: agent loop and history compaction.
/// </summary>
public class AgentSettings
{
    /// <summary>
    /// Default maximum number of model requests offering the tools per turn. Lower than the default of
    /// Microsoft.Extensions.AI (40), to bound the cost of a single turn.
    /// </summary>
    public const int kDefaultMaxIterationsPerRequest = 10;

    /// <summary>Default maximum number of consecutive failed tool call rounds before a turn fails.</summary>
    public const int kDefaultMaxConsecutiveErrorsPerRequest = 3;

    /// <summary>
    /// The default compaction reserve is the context window divided by this, i.e. a quarter of the context window.
    /// </summary>
    public const int kDefaultCompactionReserveDivisor = 4;

    /// <summary>
    /// Maximum number of model requests offering the tools per turn, the initial request included; must be at least 1.
    /// If the model still calls tools in the last of them, it is asked once more without tools for its answer.
    /// </summary>
    public int MaxIterationsPerRequest { get; set; } = kDefaultMaxIterationsPerRequest;

    /// <summary>Maximum number of consecutive failed tool call rounds before a turn fails; must not be negative.</summary>
    public int MaxConsecutiveErrorsPerRequest { get; set; } = kDefaultMaxConsecutiveErrorsPerRequest;

    /// <summary>
    /// Tokens of the context window kept free of the conversation: the older part of the conversation is summarized
    /// before a turn once it exceeds the context window minus this reserve. The reserve has to hold the instructions,
    /// the tool definitions, the new messages of the turn, the tool results of the agent loop and the answer.
    /// Must be positive and smaller than the context window, and larger than the maximum number of output tokens
    /// if that is set; a quarter of the context window if null.
    /// </summary>
    public int? CompactionReserveTokens { get; set; }

    /// <summary>
    /// Returns the compaction reserve for the given context window, see <see cref="CompactionReserveTokens"/>.
    /// </summary>
    /// <param name="contextWindowTokens">Context window of the model in tokens</param>
    internal int GetCompactionReserveTokens(int contextWindowTokens)
    {
        return CompactionReserveTokens ?? contextWindowTokens / kDefaultCompactionReserveDivisor;
    }

    /// <summary>
    /// Copies the settings, so that changes made after a factory was created do not affect it.
    /// </summary>
    internal AgentSettings CreateSnapshot()
    {
        return (AgentSettings)MemberwiseClone();
    }
}
