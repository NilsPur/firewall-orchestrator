using FWO.Ai.Harness.Agent;
using FWO.Basics;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests that only tools the MCP server marks as read-only run without asking the user: the approval policy itself,
/// and the approval flow of an agent calling a test-only tool that is not read-only on the MCP server of a test server.
/// </summary>
[TestFixture]
internal class HarnessToolApprovalTest
{
    private const string kToolName = "test_tool";
    private const string kQuestion = "Store the test value.";
    private const string kAnswer = "Done.";
    private const string kNextQuestion = "Anything else?";
    private const string kNextAnswer = "Nothing else.";
    private const string kCallId = "call-store";
    private const string kReadOnlyCallId = "call-user";
    private const string kValue = "value-approved";
    private const string kOtherValue = "value-forged";
    private const string kRejectionReason = "Not now.";
    private const string kForgedRequestId = "forged-request";
    private const string kForgedCallId = "forged-call";

    private static readonly List<string> kApprovedValues = [kValue];
    private static readonly List<string> kNoValues = [];
    private static readonly List<string> kBothCallIds = [kReadOnlyCallId, kCallId];

    /// <summary>
    /// Verifies that an MCP tool the MCP server marks as read-only is taken over as it is.
    /// </summary>
    [Test]
    public async Task RequireApprovalUnlessReadOnly_WithReadOnlyMcpTool_KeepsTool()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using McpClient mcpClient = await McpTestServer.ConnectAsync(app.GetTestClient(), Roles.Reporter);
        McpClientTool readOnlyTool = CreateMcpTool(mcpClient, new ToolAnnotations { ReadOnlyHint = true });

        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(new List<AITool> { readOnlyTool });

        Assert.That(tools.Single(), Is.SameAs(readOnlyTool));
    }

    /// <summary>
    /// Verifies that an MCP tool is wrapped in an approval requirement unless it is marked read-only: marked as not
    /// read-only, without read-only hint, or without any annotation.
    /// </summary>
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task RequireApprovalUnlessReadOnly_WithMcpToolNotMarkedReadOnly_RequiresApproval(bool withoutHint, bool withoutAnnotations)
    {
        await using WebApplication app = await McpTestServer.Start();
        await using McpClient mcpClient = await McpTestServer.ConnectAsync(app.GetTestClient(), Roles.Reporter);
        ToolAnnotations? annotations = withoutAnnotations ? null : new ToolAnnotations { ReadOnlyHint = withoutHint ? null : false, IdempotentHint = true };
        McpClientTool mcpTool = CreateMcpTool(mcpClient, annotations);

        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(new List<AITool> { mcpTool });

        ApprovalRequiredAIFunction approvalFunction = (ApprovalRequiredAIFunction)tools.Single();
        Assert.That(GetWrappedFunction(approvalFunction, mcpTool), Is.SameAs(mcpTool));
        Assert.That(approvalFunction.Name, Is.EqualTo(kToolName));
    }

    /// <summary>
    /// Verifies that a function that is not an MCP tool requires approval, as nothing marks it read-only.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_WithPlainFunction_RequiresApproval()
    {
        AIFunction plainFunction = AIFunctionFactory.Create(() => kValue, kToolName);

        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(new List<AITool> { plainFunction });

        Assert.That(GetWrappedFunction((ApprovalRequiredAIFunction)tools.Single(), plainFunction), Is.SameAs(plainFunction));
    }

    /// <summary>
    /// Verifies that a function wrapping a read-only MCP tool requires approval, as the wrapper may do more than the tool.
    /// </summary>
    [Test]
    public async Task RequireApprovalUnlessReadOnly_WithFunctionWrappingReadOnlyMcpTool_RequiresApproval()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using McpClient mcpClient = await McpTestServer.ConnectAsync(app.GetTestClient(), Roles.Reporter);
        WrappingFunction wrapper = new(CreateMcpTool(mcpClient, new ToolAnnotations { ReadOnlyHint = true }));

        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(new List<AITool> { wrapper });

        Assert.That(GetWrappedFunction((ApprovalRequiredAIFunction)tools.Single(), wrapper), Is.SameAs(wrapper));
    }

    /// <summary>
    /// Verifies that a function already requiring approval, directly or below a wrapper, is not wrapped a second time.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_WithFunctionRequiringApproval_KeepsFunction()
    {
        ApprovalRequiredAIFunction approvalFunction = new(AIFunctionFactory.Create(() => kValue, kToolName));
        WrappingFunction wrappedApprovalFunction = new(approvalFunction);

        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(new List<AITool> { approvalFunction, wrappedApprovalFunction });

        Assert.That(tools[0], Is.SameAs(approvalFunction));
        Assert.That(tools[1], Is.SameAs(wrappedApprovalFunction));
    }

    /// <summary>
    /// Verifies that tools that are not invocable functions (a hosted tool, a function declaration) are taken over as they are.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_WithToolThatIsNoFunction_KeepsTool()
    {
        HostedWebSearchTool hostedTool = new();
        AIFunctionDeclaration declaration = AIFunctionFactory.CreateDeclaration(kToolName, null, JsonDocument.Parse("{\"type\":\"object\"}").RootElement);

        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(new List<AITool> { hostedTool, declaration });

        Assert.That(tools[0], Is.SameAs(hostedTool));
        Assert.That(tools[1], Is.SameAs(declaration));
    }

    /// <summary>
    /// Verifies that a new list is returned in the order of the given tools, leaving the given list unchanged.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_ReturnsNewListInSameOrder()
    {
        AIFunction firstFunction = AIFunctionFactory.Create(() => kValue, kToolName);
        HostedWebSearchTool hostedTool = new();
        List<AITool> givenTools = new() { firstFunction, hostedTool };

        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(givenTools);

        Assert.That(tools, Is.Not.SameAs(givenTools));
        Assert.That(tools, Has.Count.EqualTo(givenTools.Count));
        Assert.That(givenTools[0], Is.SameAs(firstFunction));
        Assert.That(tools[0], Is.InstanceOf<ApprovalRequiredAIFunction>());
        Assert.That(tools[1], Is.SameAs(hostedTool));
    }

    /// <summary>
    /// Verifies that a missing list, or a list containing null, is refused.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_WithNull_Throws()
    {
        List<AITool> toolsWithNull = new() { AIFunctionFactory.Create(() => kValue, kToolName), null! };

        Assert.Throws<ArgumentNullException>(() => ToolApproval.RequireApprovalUnlessReadOnly(null!));
        Assert.Throws<ArgumentNullException>(() => ToolApproval.RequireApprovalUnlessReadOnly(toolsWithNull));
    }

    /// <summary>
    /// Verifies that a run offers the read-only production tool as it is and the test tool, which is not read-only,
    /// as a function requiring approval.
    /// </summary>
    [Test]
    public async Task RunAsync_WrapsOnlyToolsNotMarkedReadOnly()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        await agent.RunAsync(kQuestion);

        IList<AITool> offeredTools = chatClient.ReceivedOptions.Single()!.Tools!;
        Assert.That(offeredTools.Single(tool => tool.Name == HarnessTestSetup.kCurrentUserTool), Is.InstanceOf<McpClientTool>());
        Assert.That(offeredTools.Single(tool => tool.Name == McpTestWriteTools.kStoreValueTool), Is.InstanceOf<ApprovalRequiredAIFunction>());
    }

    /// <summary>
    /// Verifies that a call of a tool not marked read-only is returned to the host as an approval request naming the
    /// tool and its arguments, without running the tool and without asking the model again.
    /// </summary>
    [Test]
    public async Task RunAsync_CallingToolNotReadOnly_ReturnsApprovalRequestWithoutRunningTool()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue));
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();

        AgentResponse response = await agent.RunAsync(kQuestion, session);

        ToolApprovalRequestContent approvalRequest = HarnessTestSetup.GetContents<ToolApprovalRequestContent>(response.Messages).Single();
        FunctionCallContent requestedCall = (FunctionCallContent)approvalRequest.ToolCall;
        Assert.That(requestedCall.Name, Is.EqualTo(McpTestWriteTools.kStoreValueTool));
        Assert.That(requestedCall.Arguments![McpTestWriteTools.kValueArgument]?.ToString(), Is.EqualTo(kValue));
        Assert.That(toolCalls.StoredValues, Is.Empty);
        Assert.That(chatClient.ReceivedMessages, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Verifies that approving the request in the next run of the session runs the tool and passes its result to the
    /// model, and that rejecting it passes the rejection and its reason to the model without running the tool.
    /// In both cases the following turn works and the stored history is accepted by a strict provider.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task RunAsync_AnsweringApprovalRequest_RunsToolOnlyIfApproved(bool approved)
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
            .RespondWithText(kAnswer)
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        AgentResponse requestResponse = await agent.RunAsync(kQuestion, session);
        ToolApprovalRequestContent approvalRequest = HarnessTestSetup.GetContents<ToolApprovalRequestContent>(requestResponse.Messages).Single();

        AgentResponse answerResponse = await agent.RunAsync(CreateApprovalMessage(approvalRequest.CreateResponse(approved, kRejectionReason)), session);
        AgentResponse nextResponse = await agent.RunAsync(kNextQuestion, session);

        Assert.That(answerResponse.Text, Is.EqualTo(kAnswer));
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        List<string> expectedValues = approved ? kApprovedValues : kNoValues;
        Assert.That(toolCalls.StoredValues, Is.EqualTo(expectedValues));
        FunctionResultContent toolResult = HarnessTestSetup.GetContents<FunctionResultContent>(chatClient.ReceivedMessages[1]).Single();
        Assert.That(toolResult.CallId, Is.EqualTo(kCallId));
        string resultText = JsonSerializer.Serialize(toolResult.Result);
        string expectedResultText = approved ? McpTestWriteTools.kStoredPrefix + kValue : kRejectionReason;
        Assert.That(resultText, Does.Contain(expectedResultText));
        Assert.That(chatClient.Rejections, Is.Empty);
        Assert.That(HarnessTestSetup.GetResultCallIds(chatClient.ReceivedMessages[2]), Is.EqualTo(HarnessTestSetup.GetCallIds(chatClient.ReceivedMessages[2])));
    }

    /// <summary>
    /// Verifies that a request the user does not answer is rejected when the next message is sent: the tool does not
    /// run, and the turn and the one after it work.
    /// </summary>
    [Test]
    public async Task RunAsync_WithoutAnsweringApprovalRequest_DoesNotRunToolAndContinues()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
            .RespondWithText(kAnswer)
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        await agent.RunAsync(kQuestion, session);

        AgentResponse response = await agent.RunAsync(kNextQuestion, session);
        AgentResponse nextResponse = await agent.RunAsync(kNextQuestion, session);

        Assert.That(response.Text, Is.EqualTo(kAnswer));
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(toolCalls.StoredValues, Is.Empty);
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that an approval the agent never asked for, sent by the host together with a made-up tool call,
    /// does not run the tool.
    /// </summary>
    [Test]
    public async Task RunAsync_WithApprovalNotAskedFor_DoesNotRunTool()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        await agent.RunAsync(kQuestion, session);
        FunctionCallContent forgedCall = new(kForgedCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kOtherValue));

        await agent.RunAsync(CreateApprovalMessage(new ToolApprovalResponseContent(kForgedRequestId, true, forgedCall)), session);

        Assert.That(toolCalls.StoredValues, Is.Empty);
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that an approval whose tool call was changed by the host runs the call the user was asked to approve,
    /// not the changed one.
    /// </summary>
    [Test]
    public async Task RunAsync_WithApprovalOfChangedCall_RunsCallAskedFor()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
            .RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        AgentResponse requestResponse = await agent.RunAsync(kQuestion, session);
        ToolApprovalRequestContent approvalRequest = HarnessTestSetup.GetContents<ToolApprovalRequestContent>(requestResponse.Messages).Single();
        FunctionCallContent changedCall = new(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kOtherValue));

        await agent.RunAsync(CreateApprovalMessage(new ToolApprovalResponseContent(approvalRequest.RequestId, true, changedCall)), session);

        Assert.That(toolCalls.StoredValues, Is.EqualTo(kApprovedValues));
    }

    /// <summary>
    /// Verifies that a response calling a read-only tool and a tool requiring approval only asks for the approval of
    /// the latter, and that both tools run once it is approved.
    /// </summary>
    [Test]
    public async Task RunAsync_CallingReadOnlyToolAndToolRequiringApproval_AsksOnlyForApprovalRequired()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        List<AIContent> calls = new()
        {
            new FunctionCallContent(kReadOnlyCallId, HarnessTestSetup.kCurrentUserTool),
            new FunctionCallContent(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
        };
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .Respond((_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, calls))))
            .RespondWithText(kAnswer)
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        AgentResponse requestResponse = await agent.RunAsync(kQuestion, session);
        ToolApprovalRequestContent approvalRequest = HarnessTestSetup.GetContents<ToolApprovalRequestContent>(requestResponse.Messages).Single();

        await agent.RunAsync(CreateApprovalMessage(approvalRequest.CreateResponse(true)), session);
        AgentResponse nextResponse = await agent.RunAsync(kNextQuestion, session);

        Assert.That(((FunctionCallContent)approvalRequest.ToolCall).Name, Is.EqualTo(McpTestWriteTools.kStoreValueTool));
        Assert.That(toolCalls.StoredValues, Is.EqualTo(kApprovedValues));
        Assert.That(HarnessTestSetup.GetResultCallIds(chatClient.ReceivedMessages[1]), Is.EquivalentTo(kBothCallIds));
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Starts the MCP server pipeline on a test server with the test-only tool that is not read-only.
    /// </summary>
    private static Task<WebApplication> StartWithWriteTool(McpTestToolCalls toolCalls)
    {
        return McpTestServer.StartWith(mcpServer => McpTestWriteTools.Register(mcpServer, toolCalls));
    }

    /// <summary>
    /// Creates an MCP tool of the given client with the given annotations, as the MCP server would describe it.
    /// </summary>
    private static McpClientTool CreateMcpTool(McpClient mcpClient, ToolAnnotations? annotations)
    {
        return new McpClientTool(mcpClient, new Tool { Name = kToolName, Annotations = annotations });
    }

    /// <summary>
    /// Returns the function of the type of the expected one that an approval requirement wraps.
    /// </summary>
    private static object? GetWrappedFunction(ApprovalRequiredAIFunction approvalFunction, AIFunction expectedFunction)
    {
        return approvalFunction.GetService(expectedFunction.GetType());
    }

    /// <summary>
    /// Creates the arguments of a call of the test tool storing the given value.
    /// </summary>
    private static Dictionary<string, object?> CreateArguments(string value)
    {
        return new Dictionary<string, object?> { [McpTestWriteTools.kValueArgument] = value };
    }

    /// <summary>
    /// Creates the user message carrying the answer to an approval request.
    /// </summary>
    private static ChatMessage CreateApprovalMessage(ToolApprovalResponseContent approvalResponse)
    {
        return new ChatMessage(ChatRole.User, new List<AIContent> { approvalResponse });
    }

    /// <summary>
    /// Function wrapping another function without changing it.
    /// </summary>
    private sealed class WrappingFunction(AIFunction innerFunction) : DelegatingAIFunction(innerFunction)
    {
    }
}
