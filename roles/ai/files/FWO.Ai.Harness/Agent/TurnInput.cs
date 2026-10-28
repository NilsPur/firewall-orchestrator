using AGUI.Abstractions;
using AGUI.Server;
using FWO.Ai.Harness.Mcp;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Input of a turn: the messages the agent stores in the session's history and sends to the model. The harness keeps
/// the conversation in the session, so a client only needs to send what is new; an AG-UI client may send its whole
/// conversation with every run, which is ignored. Of the messages of a run, the turn takes:
/// <list type="bullet">
/// <item>the answers to tool approval requests (<see cref="ToolApprovalResponseContent"/>), which AG-UI sends as
/// resume entries; the agent only honors the answer to a request it made itself and recorded in the session,</item>
/// <item>the text of the user messages after the last message of another role (the new user input), or instead the
/// messages of the MCP prompt the run names, see <see cref="PromptRequest"/>.</item>
/// </list>
/// Everything else a client sends, i.e. its history, system messages, tool calls and results, other content, and the
/// AG-UI state and context, is ignored, so a client can neither rewrite the stored conversation nor add instructions.
/// </summary>
internal sealed class TurnInput
{
    /// <summary>Maximum length of the text of a turn, and of each prompt argument.</summary>
    internal const int kMaxTextLength = 32000;

    private readonly ChatMessage? approvalAnswers;
    private readonly ChatMessage? userText;
    private readonly PromptRequest? prompt;

    /// <summary>
    /// Creates the input from its parts, see <see cref="Read"/>.
    /// </summary>
    private TurnInput(ChatMessage? approvalAnswers, ChatMessage? userText, PromptRequest? prompt)
    {
        this.approvalAnswers = approvalAnswers;
        this.userText = userText;
        this.prompt = prompt;
    }

    /// <summary>
    /// Reads the input of a turn from the messages of a run and, for an AG-UI run, from the AG-UI input the hosting
    /// passes in the run options.
    /// </summary>
    /// <param name="messages">Messages of the run</param>
    /// <param name="options">Run options; for an AG-UI run they carry the AG-UI input</param>
    /// <exception cref="ArgumentException">The run has no new input, too long a text, both a text and a prompt, or a malformed prompt.</exception>
    /// <exception cref="NotSupportedException">The AG-UI input declares client tools.</exception>
    internal static TurnInput Read(IEnumerable<ChatMessage> messages, AgentRunOptions? options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        RunAgentInput? aguiInput = GetAguiInput(options);
        // client tools would also change how the AG-UI hosting reports the approval requests of the harness's tools
        if (aguiInput?.Tools is { Count: > 0 })
        {
            throw new NotSupportedException("The harness does not run client tools.");
        }
        List<ChatMessage> messageList = messages.ToList();
        ChatMessage? approvalAnswers = CollectApprovalAnswers(messageList);
        ChatMessage? userText = CollectNewUserText(messageList);
        PromptRequest? prompt = PromptRequest.FromForwardedProperties(aguiInput?.ForwardedProperties);
        if (prompt != null && userText != null)
        {
            throw new ArgumentException("A turn either names a prompt or sends a text, not both.", nameof(messages));
        }
        if (approvalAnswers == null && userText == null && prompt == null)
        {
            throw new ArgumentException("The turn contains no new user input.", nameof(messages));
        }
        return new TurnInput(approvalAnswers, userText, prompt);
    }

    /// <summary>
    /// Returns the messages of the turn: the approval answers first, so the tool results they cause come before a
    /// new question, then the user text or the messages of the prompt.
    /// </summary>
    /// <param name="mcpConnection">Connection to the MCP server with the user's JWT, to get the prompt from</param>
    /// <param name="cancellationToken">Cancels getting the prompt</param>
    /// <exception cref="InvalidOperationException">The prompt has no messages.</exception>
    internal async Task<List<ChatMessage>> GetMessagesAsync(FwoMcpConnection mcpConnection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mcpConnection);
        List<ChatMessage> turnMessages = new();
        if (approvalAnswers != null)
        {
            turnMessages.Add(approvalAnswers);
        }
        if (userText != null)
        {
            turnMessages.Add(userText);
        }
        if (prompt != null)
        {
            List<ChatMessage> promptMessages = await mcpConnection.GetPromptMessagesAsync(prompt.Name, prompt.Arguments, cancellationToken);
            if (promptMessages.Count == 0)
            {
                throw new InvalidOperationException("The prompt has no messages.");
            }
            turnMessages.AddRange(promptMessages);
        }
        return turnMessages;
    }

    /// <summary>
    /// Returns the AG-UI input the AG-UI hosting passes in the chat options of the run options, or null for other runs.
    /// </summary>
    private static RunAgentInput? GetAguiInput(AgentRunOptions? options)
    {
        ChatOptions? chatOptions = (options as ChatClientAgentRunOptions)?.ChatOptions;
        return chatOptions != null && chatOptions.TryGetRunAgentInput(out RunAgentInput? aguiInput) ? aguiInput : null;
    }

    /// <summary>
    /// Returns one user message with the answers to tool approval requests in the user messages, or null if there is none.
    /// </summary>
    private static ChatMessage? CollectApprovalAnswers(List<ChatMessage> messages)
    {
        List<AIContent> answers = messages
            .Where(message => message.Role == ChatRole.User)
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalResponseContent>()
            .Cast<AIContent>()
            .ToList();
        return answers.Count > 0 ? new ChatMessage(ChatRole.User, answers) : null;
    }

    /// <summary>
    /// Returns one user message with the text of the new user input, or null if there is none. The new input are the
    /// user messages after the last message of another role, not counting the messages holding only approval requests
    /// or answers, which the AG-UI hosting appends for the resume entries.
    /// </summary>
    /// <exception cref="ArgumentException">The text is too long.</exception>
    private static ChatMessage? CollectNewUserText(List<ChatMessage> messages)
    {
        List<ChatMessage> conversation = messages.Where(message => !HoldsApprovalsOnly(message)).ToList();
        int firstNewMessage = conversation.Count;
        while (firstNewMessage > 0 && conversation[firstNewMessage - 1].Role == ChatRole.User)
        {
            firstNewMessage--;
        }
        // new text contents, so that nothing but the text of the client is taken over
        List<AIContent> texts = conversation
            .Skip(firstNewMessage)
            .SelectMany(message => message.Contents)
            .OfType<TextContent>()
            .Where(text => !string.IsNullOrWhiteSpace(text.Text))
            .Select(text => new TextContent(text.Text))
            .Cast<AIContent>()
            .ToList();
        if (texts.Count == 0)
        {
            return null;
        }
        int textLength = texts.Sum(text => ((TextContent)text).Text.Length);
        if (textLength > kMaxTextLength)
        {
            throw new ArgumentException($"The text of a turn must not be longer than {kMaxTextLength} characters.", nameof(messages));
        }
        return new ChatMessage(ChatRole.User, texts);
    }

    /// <summary>
    /// Tells whether a message holds nothing but tool approval requests and answers.
    /// </summary>
    private static bool HoldsApprovalsOnly(ChatMessage message)
    {
        return message.Contents.Count > 0
            && message.Contents.All(content => content is ToolApprovalRequestContent or ToolApprovalResponseContent);
    }
}
