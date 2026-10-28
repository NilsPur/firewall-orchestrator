using FWO.Ai.Harness.Agent;
using FWO.Basics;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the turns of a user agent through the MCP server pipeline running on a test server: tool calls with the
/// user's JWT and roles, failed and cancelled turns, streaming, the MCP prompts and the stored history.
/// The AI provider is replaced by a <see cref="ScriptedChatClient"/>, strict where the stored history matters.
/// </summary>
[TestFixture]
internal class HarnessFwoUserAgentTest
{
    private const string kQuestion = "Which roles do I have?";
    private const string kNextQuestion = "And now?";
    private const string kAnswer = "You are a reporter.";
    private const string kNextAnswer = "Still a reporter.";
    private const string kCallId = "call-1";
    private const string kOtherUserName = "other-user";
    private const string kProviderFailure = "provider unavailable";
    private const string kReasonWithMarkup = "Reads <b>customer data</b> for order checks.";
    private const string kEscapedReasonWithMarkup = "Reads &lt;b&gt;customer data&lt;/b&gt; for order checks.";
    private const string kUnknownPrompt = "unknown_prompt";
    private const string kFirstInstruction = "You support FWO users.";
    private const string kSecondInstruction = "Keep UIDs exactly as given.";

    private static readonly List<ChatRole> kToolTurnRoles = [ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant];
    private static readonly List<string> kQuestionAndAnswer = [kQuestion, kAnswer];
    private static readonly List<string> kSecondTurnRequest = [kQuestion, kAnswer, kNextQuestion];
    private static readonly List<ChatMessage> kNoMessages = [];
    private static readonly List<ChatMessage> kInstructionMessages = [new(ChatRole.User, kFirstInstruction), new(ChatRole.User, kSecondInstruction)];
    private static readonly List<ChatMessage> kBlankInstructionMessages = [new(ChatRole.User, " "), new(ChatRole.User, "")];

    /// <summary>
    /// Verifies that a tool call of the model runs on the MCP server with the user's JWT, its result reaches the model,
    /// and the history keeps the whole exchange.
    /// </summary>
    [Test]
    public async Task RunAsync_WithToolCall_RunsToolWithUsersTokenAndStoresExchange()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool).RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();

        AgentResponse response = await agent.RunAsync(kQuestion, session);

        Assert.That(response.Text, Is.EqualTo(kAnswer));
        FunctionResultContent toolResult = HarnessTestSetup.GetContents<FunctionResultContent>(chatClient.ReceivedMessages[1]).Single();
        Assert.That(toolResult.CallId, Is.EqualTo(kCallId));
        string resultJson = JsonSerializer.Serialize(toolResult.Result);
        Assert.That(resultJson, Does.Contain(McpTestServer.kUserName));
        Assert.That(resultJson, Does.Contain(Roles.Reporter));
        IReadOnlyList<ChatMessage> history = factory.GetHistory(session);
        Assert.That(history.Select(message => message.Role), Is.EqualTo(kToolTurnRoles));
        Assert.That(history[1].Contents.OfType<FunctionCallContent>().Single().Name, Is.EqualTo(HarnessTestSetup.kCurrentUserTool));
        Assert.That(history[2].Contents.OfType<FunctionResultContent>().Single().CallId, Is.EqualTo(kCallId));
        Assert.That(history[3].Text, Is.EqualTo(kAnswer));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a run only offers the tools of the user's roles: the random number tool only to admins and auditors.
    /// </summary>
    [TestCase(Roles.Reporter, false)]
    [TestCase(Roles.Recertifier, false)]
    [TestCase(Roles.Admin, true)]
    [TestCase(Roles.Auditor, true)]
    public async Task RunAsync_OffersToolsOfUsersRoles(string role, bool expectRandomNumberTool)
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, role);

        await agent.RunAsync(kQuestion);

        List<string> toolNames = HarnessTestSetup.GetToolNames(chatClient.ReceivedOptions.Single());
        Assert.That(toolNames, Does.Contain(HarnessTestSetup.kCurrentUserTool));
        Assert.That(toolNames.Contains(HarnessTestSetup.kRandomNumberTool), Is.EqualTo(expectRandomNumberTool));
    }

    /// <summary>
    /// Verifies that a tool the user's roles do not offer is not run when the model calls it anyway; the model gets
    /// an error instead of a result.
    /// </summary>
    [Test]
    public async Task RunAsync_ModelCallingToolNotOffered_DoesNotRunIt()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kRandomNumberTool).RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        AgentResponse response = await agent.RunAsync(kQuestion);

        Assert.That(response.Text, Is.EqualTo(kAnswer));
        FunctionResultContent toolResult = HarnessTestSetup.GetContents<FunctionResultContent>(chatClient.ReceivedMessages[1]).Single();
        Assert.That(toolResult.Result, Is.InstanceOf<string>());
        Assert.That(int.TryParse((string)toolResult.Result!, out _), Is.False);
    }

    /// <summary>
    /// Verifies that the agents of different users created by one factory call the tools with their own user's JWT.
    /// </summary>
    [Test]
    public async Task RunAsync_AgentsOfDifferentUsers_CallToolsWithOwnToken()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool).RespondWithText(kAnswer)
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool).RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent defaultUserAgent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        string otherUserToken = McpTestServer.CreateUserToken(kOtherUserName, Roles.Admin);
        FwoUserAgent otherUserAgent = HarnessTestSetup.CreateUserAgent(factory, _ => ValueTask.FromResult(otherUserToken));

        await defaultUserAgent.RunAsync(kQuestion);
        await otherUserAgent.RunAsync(kQuestion);

        string defaultUserResult = GetToolResultJson(chatClient.ReceivedMessages[1]);
        string otherUserResult = GetToolResultJson(chatClient.ReceivedMessages[3]);
        Assert.That(defaultUserResult, Does.Contain(McpTestServer.kUserName));
        Assert.That(defaultUserResult, Does.Not.Contain(kOtherUserName));
        Assert.That(otherUserResult, Does.Contain(kOtherUserName));
        Assert.That(otherUserResult, Does.Contain(Roles.Admin));
    }

    /// <summary>
    /// Verifies that a session stored after one request is continued by the user agent of the next request, as the
    /// sessions belong to the shared agent: the second run sends the first turn along.
    /// </summary>
    [Test]
    public async Task RunAsync_NewUserAgentOnSerializedSession_ContinuesConversation()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent firstRequestAgent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await firstRequestAgent.CreateSessionAsync();
        await firstRequestAgent.RunAsync(kQuestion, session);
        JsonElement storedSession = await firstRequestAgent.SerializeSessionAsync(session);
        FwoUserAgent nextRequestAgent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession restoredSession = await nextRequestAgent.DeserializeSessionAsync(storedSession);

        AgentResponse response = await nextRequestAgent.RunAsync(kNextQuestion, restoredSession);

        Assert.That(response.Text, Is.EqualTo(kNextAnswer));
        Assert.That(nextRequestAgent.Name, Is.EqualTo(FwoAgentFactory.kAgentName));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kSecondTurnRequest));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that the sessions keep separate histories.
    /// </summary>
    [Test]
    public async Task RunAsync_OnDifferentSessions_KeepsHistoriesApart()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession firstSession = await agent.CreateSessionAsync();
        AgentSession secondSession = await agent.CreateSessionAsync();

        await agent.RunAsync(kQuestion, firstSession);
        await agent.RunAsync(kNextQuestion, secondSession);

        Assert.That(chatClient.ReceivedMessages[1].Single().Text, Is.EqualTo(kNextQuestion));
        Assert.That(HarnessTestSetup.GetTexts(factory.GetHistory(firstSession)), Is.EqualTo(kQuestionAndAnswer));
        Assert.That(factory.GetHistory(secondSession), Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Verifies that the conversation a client sends again is ignored: only the new user message is sent along with
    /// the stored history, and stored once.
    /// </summary>
    [Test]
    public async Task RunAsync_WithConversationSentAgain_SendsOnlyNewUserMessage()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        await agent.RunAsync(kQuestion, session);
        List<ChatMessage> clientConversation =
        [
            new ChatMessage(ChatRole.User, kQuestion),
            new ChatMessage(ChatRole.Assistant, kAnswer),
            new ChatMessage(ChatRole.User, kNextQuestion)
        ];

        await agent.RunAsync(clientConversation, session);

        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kSecondTurnRequest));
        Assert.That(factory.GetHistory(session), Has.Count.EqualTo(kQuestionAndAnswer.Count * 2));
    }

    /// <summary>
    /// Verifies that a failing request leaves the stored history as it was before the turn, and the next turn works.
    /// </summary>
    [Test]
    public async Task RunAsync_WhenProviderFails_KeepsHistoryAndRunsNextTurn()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithText(kAnswer)
            .Respond((_, _, _) => Task.FromException<ChatResponse>(new InvalidOperationException(kProviderFailure)))
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        await agent.RunAsync(kQuestion, session);
        List<ChatMessage> historyBeforeTurn = factory.GetHistory(session).ToList();

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await agent.RunAsync(kNextQuestion, session))!;

        Assert.That(exception.Message, Is.EqualTo(kProviderFailure));
        Assert.That(factory.GetHistory(session), Is.EqualTo(historyBeforeTurn));
        AgentResponse nextResponse = await agent.RunAsync(kNextQuestion, session);
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kSecondTurnRequest));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a cancelled turn, here while a tool call is in progress, leaves the stored history as it was
    /// before the turn, and the next turn works.
    /// </summary>
    [Test]
    public async Task RunAsync_WhenCancelled_KeepsHistoryAndRunsNextTurn()
    {
        await using WebApplication app = await McpTestServer.Start();
        BlockingAnswer blockingAnswer = new();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithText(kAnswer)
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool)
            .Respond(blockingAnswer.AnswerAsync)
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        await agent.RunAsync(kQuestion, session);
        List<ChatMessage> historyBeforeTurn = factory.GetHistory(session).ToList();
        using CancellationTokenSource cancellation = new();
        Task<AgentResponse> cancelledTurn = agent.RunAsync(kNextQuestion, session, cancellationToken: cancellation.Token);
        await blockingAnswer.Started;

        await cancellation.CancelAsync();

        Assert.CatchAsync<OperationCanceledException>(async () => await cancelledTurn);
        Assert.That(factory.GetHistory(session), Is.EqualTo(historyBeforeTurn));
        AgentResponse nextResponse = await agent.RunAsync(kNextQuestion, session);
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kSecondTurnRequest));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a blank user message is refused without asking the model.
    /// </summary>
    [TestCase("")]
    [TestCase(" ")]
    public async Task RunAsync_WithBlankMessage_ThrowsWithoutAskingModel(string userMessage)
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();

        Assert.CatchAsync<ArgumentException>(async () => await agent.RunAsync(userMessage, session));

        Assert.That(chatClient.ReceivedMessages, Is.Empty);
        Assert.That(factory.GetHistory(session), Is.Empty);
    }

    /// <summary>
    /// Verifies that a streamed turn with a tool call returns the answer as updates and stores the same history as a
    /// turn that is not streamed.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_WithToolCall_StoresExchange()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool).RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();

        List<AgentResponseUpdate> updates = new();
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(kQuestion, session))
        {
            updates.Add(update);
        }

        Assert.That(updates.ToAgentResponse().Text, Is.EqualTo(kAnswer));
        Assert.That(factory.GetHistory(session).Select(message => message.Role), Is.EqualTo(kToolTurnRoles));
        AgentResponse nextResponse = await agent.RunAsync(kNextQuestion, session);
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that the messages of a prompt the run names, with the arguments escaped by the MCP server, are sent
    /// and stored as the user turn.
    /// </summary>
    [Test]
    public async Task RunAsync_WithPrompt_SendsAndStoresPromptAsUserTurn()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Requester);
        AgentSession session = await agent.CreateSessionAsync();
        Dictionary<string, string> arguments = new() { [HarnessTestSetup.kReasonArgument] = kReasonWithMarkup };

        AgentResponse response = await agent.RunAsync(kNoMessages, session,
            HarnessTestSetup.CreatePromptRunOptions(HarnessTestSetup.kAccessRequestPrompt, arguments));

        Assert.That(response.Text, Is.EqualTo(kAnswer));
        ChatMessage sentMessage = chatClient.ReceivedMessages.Single().Single();
        Assert.That(sentMessage.Role, Is.EqualTo(ChatRole.User));
        Assert.That(sentMessage.Text, Does.Contain(kEscapedReasonWithMarkup));
        Assert.That(sentMessage.Text, Does.Not.Contain(kReasonWithMarkup));
        Assert.That(factory.GetHistory(session)[0].Text, Is.EqualTo(sentMessage.Text));
    }

    /// <summary>
    /// Verifies that the AG-UI input of a run does not reach the AI provider: the options of the model request carry
    /// only the tools of the user, and nothing the client sent along.
    /// </summary>
    [Test]
    public async Task RunAsync_WithAguiInput_DoesNotPassClientOptionsToProvider()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        await agent.RunAsync(kQuestion, options: HarnessTestSetup.CreateAguiRunOptions(new AGUI.Abstractions.RunAgentInput()));

        Assert.That(chatClient.ReceivedOptions.Single()?.AdditionalProperties, Is.Null.Or.Empty);
    }

    /// <summary>
    /// Verifies that an unknown prompt is refused by the MCP server without asking the model.
    /// </summary>
    [Test]
    public async Task RunAsync_WithUnknownPrompt_ThrowsWithoutAskingModel()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Requester);

        Assert.CatchAsync<McpException>(async () => await agent.RunAsync(kNoMessages, options: HarnessTestSetup.CreatePromptRunOptions(kUnknownPrompt)));

        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that a prompt without messages is refused, as it cannot be sent as a turn.
    /// </summary>
    [Test]
    public async Task RunAsync_WithPromptWithoutMessages_ThrowsWithoutAskingModel()
    {
        await using WebApplication app = await McpTestServer.StartWith(mcpServer => mcpServer.WithPrompts<McpTestPrompts>());
        ScriptedChatClient chatClient = new();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Requester);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await agent.RunAsync(kNoMessages, options: HarnessTestSetup.CreatePromptRunOptions(McpTestPrompts.kEmptyPrompt)));

        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that the history of a new session is empty, and that the history returned is a copy that later turns
    /// do not change.
    /// </summary>
    [Test]
    public async Task GetHistory_ReturnsCopyOfStoredHistory()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        IReadOnlyList<ChatMessage> emptyHistory = factory.GetHistory(session);
        await agent.RunAsync(kQuestion, session);
        IReadOnlyList<ChatMessage> historyAfterFirstTurn = factory.GetHistory(session);

        await agent.RunAsync(kNextQuestion, session);

        Assert.That(emptyHistory, Is.Empty);
        Assert.That(HarnessTestSetup.GetTexts(historyAfterFirstTurn), Is.EqualTo(kQuestionAndAnswer));
        Assert.That(factory.GetHistory(session), Has.Count.EqualTo(kQuestionAndAnswer.Count * 2));
        Assert.That(factory.GetHistory(session), Is.Not.SameAs(factory.GetHistory(session)));
    }

    /// <summary>
    /// Verifies that a missing session is refused.
    /// </summary>
    [Test]
    public async Task GetHistory_WithoutSession_Throws()
    {
        await using WebApplication app = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient());

        Assert.Throws<ArgumentNullException>(() => factory.GetHistory(null!));
    }

    /// <summary>
    /// Returns the result of the single tool call in a request, as JSON.
    /// </summary>
    private static string GetToolResultJson(List<ChatMessage> request)
    {
        FunctionResultContent toolResult = HarnessTestSetup.GetContents<FunctionResultContent>(request).Single();
        return JsonSerializer.Serialize(toolResult.Result);
    }

    /// <summary>
    /// Verifies that the texts of the messages of the instructions prompt are joined line by line.
    /// </summary>
    [Test]
    public void JoinInstructions_WithSeveralMessages_JoinsTextsByLine()
    {
        string? instructions = FwoUserAgent.JoinInstructions(kInstructionMessages);

        Assert.That(instructions, Is.EqualTo(kFirstInstruction + Environment.NewLine + kSecondInstruction));
    }

    /// <summary>
    /// Verifies that an instructions prompt without text gives no instructions, so that no empty instructions are sent.
    /// </summary>
    [Test]
    public void JoinInstructions_WithoutText_ReturnsNull()
    {
        Assert.That(FwoUserAgent.JoinInstructions(kNoMessages), Is.Null);
        Assert.That(FwoUserAgent.JoinInstructions(kBlankInstructionMessages), Is.Null);
    }
}
