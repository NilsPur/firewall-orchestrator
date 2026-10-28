using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using JwtConstants = FWO.Basics.JwtConstants;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the AG-UI endpoint of the harness server on a test server, with a scripted chat client instead of an AI
/// provider and the MCP server pipeline on another test server: authorization, the event stream, conversations kept
/// on the server per user and thread, tool approval through interrupts and resume entries, MCP prompts, runs on several
/// threads at the same time, and the limits.
/// </summary>
[TestFixture]
internal class HarnessServerTest
{
    private const string kThreadId = "thread-1";
    private const string kOtherThreadId = "thread-2";
    private const string kQuestion = "Which roles do I have?";
    private const string kNextQuestion = "And now?";
    private const string kAnswer = "You are a reporter.";
    private const string kNextAnswer = "Still a reporter.";
    private const string kCallId = "call-store";
    private const string kValue = "value-approved";
    private const string kOtherValue = "value-forged";
    private const string kForgedInterruptId = "forged-interrupt";
    private const string kOtherUserName = "other-user";
    private const string kReason = "Needed for the monthly payroll export to the bank.";
    private const string kClientTool = "client_tool";
    private const string kProviderFailure = "provider unavailable";
    private const int kSingleRunPerMinute = 1;
    private const int kSingleConcurrentRun = 1;
    private const int kTwoConcurrentRuns = 2;
    private const int kTokenLifetimeMinutes = 5;

    private static readonly List<string> kApprovedValues = [kValue];
    private static readonly List<string> kNoValues = [];
    private static readonly List<string> kFirstAndSecondTurn = [kQuestion, kAnswer, kNextQuestion];
    private static readonly List<string> kSecondTurnOnly = [kNextQuestion];

    /// <summary>
    /// Verifies that a run without a token is rejected without asking the model.
    /// </summary>
    [Test]
    public async Task Run_WithoutToken_IsRejectedWith401()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);

        using HttpResponseMessage response = await HarnessTestServer.PostRun(app.GetTestClient(), null, HarnessTestServer.CreateRunBody(kThreadId, kQuestion));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that the tokens of technical users and anonymous bootstrap tokens do not give access to the agent.
    /// </summary>
    [TestCase(Roles.Anonymous)]
    [TestCase(Roles.MiddlewareServer)]
    [TestCase(Roles.Importer)]
    public async Task Run_WithTechnicalOrAnonymousRole_IsRejectedWith403(string role)
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);

        using HttpResponseMessage response = await HarnessTestServer.PostRun(app.GetTestClient(), McpTestServer.CreateMiddlewareToken(role),
            HarnessTestServer.CreateRunBody(kThreadId, kQuestion));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that a token without the user's directory identity is rejected, as the sessions are isolated by it.
    /// </summary>
    [Test]
    public async Task Run_WithoutUserDn_IsRejectedWith403()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);

        using HttpResponseMessage response = await HarnessTestServer.PostRun(app.GetTestClient(), CreateTokenWithoutDn(Roles.Reporter),
            HarnessTestServer.CreateRunBody(kThreadId, kQuestion));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that a run streams the answer as AG-UI events, from the start to the end of the run.
    /// </summary>
    [Test]
    public async Task Run_StreamsAnswerAsAguiEvents()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);

        List<JsonElement> events = await HarnessTestServer.RunAsync(app.GetTestClient(), McpTestServer.CreateMiddlewareToken(Roles.Reporter), kThreadId, kQuestion);

        List<string> eventTypes = HarnessTestServer.GetEventTypes(events);
        Assert.That(eventTypes[0], Is.EqualTo(HarnessTestServer.kRunStarted));
        Assert.That(eventTypes[^1], Is.EqualTo(HarnessTestServer.kRunFinished));
        Assert.That(HarnessTestServer.GetText(events), Is.EqualTo(kAnswer));
        Assert.That(chatClient.ReceivedMessages.Single().Single().Text, Is.EqualTo(kQuestion));
    }

    /// <summary>
    /// Verifies that the next run on the same thread continues the conversation the server keeps, when the client
    /// sends only the new message and when it sends its whole conversation again.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task Run_OnSameThread_ContinuesConversationKeptOnServer(bool sendConversationAgain)
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        HttpClient httpClient = app.GetTestClient();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        await HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kQuestion);
        JsonObject nextRun = HarnessTestServer.CreateRunInput(kThreadId, kNextQuestion);
        if (sendConversationAgain)
        {
            nextRun["messages"] = CreateConversation(kQuestion, kAnswer, kNextQuestion);
        }

        using HttpResponseMessage response = await HarnessTestServer.PostRun(httpClient, accessToken, nextRun.ToJsonString());

        Assert.That(HarnessTestServer.GetText(await HarnessTestServer.ReadEventsAsync(response)), Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kFirstAndSecondTurn));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that the conversations are kept per user: another user sending the thread id of a conversation does
    /// not get into it, but starts a conversation of their own.
    /// </summary>
    [Test]
    public async Task Run_OfOtherUserOnSameThread_StartsOwnConversation()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        HttpClient httpClient = app.GetTestClient();
        await HarnessTestServer.RunAsync(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter), kThreadId, kQuestion);

        await HarnessTestServer.RunAsync(httpClient, McpTestServer.CreateUserToken(kOtherUserName, Roles.Reporter), kThreadId, kNextQuestion);

        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kSecondTurnOnly));
    }

    /// <summary>
    /// Verifies the approval of a tool that is not read-only: the run ends with an interrupt naming the tool call,
    /// without running the tool, and the resume entry answering it runs the tool only if approved.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task Run_CallingToolNotReadOnly_InterruptsAndRunsToolOnlyIfApproved(bool approved)
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication mcpApp = await McpTestServer.StartWith(mcpServer => McpTestWriteTools.Register(mcpServer, toolCalls));
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
            .RespondWithText(kAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        HttpClient httpClient = app.GetTestClient();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);

        List<JsonElement> requestEvents = await HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kQuestion);
        string interruptId = GetInterruptId(requestEvents);
        Assert.That(HarnessTestServer.GetEventTypes(requestEvents), Does.Contain(HarnessTestServer.kToolCallStart));
        Assert.That(toolCalls.StoredValues, Is.Empty);

        JsonObject resumeRun = CreateResumeRun(interruptId, approved, kValue);
        using HttpResponseMessage response = await HarnessTestServer.PostRun(httpClient, accessToken, resumeRun.ToJsonString());

        Assert.That(HarnessTestServer.GetText(await HarnessTestServer.ReadEventsAsync(response)), Is.EqualTo(kAnswer));
        Assert.That(toolCalls.StoredValues, Is.EqualTo(approved ? kApprovedValues : kNoValues));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that the resume entry of a client can neither approve a tool call the agent never asked for nor change
    /// the arguments of the call it asked for.
    /// </summary>
    [Test]
    public async Task Run_WithForgedOrChangedApproval_RunsOnlyCallAskedFor()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication mcpApp = await McpTestServer.StartWith(mcpServer => McpTestWriteTools.Register(mcpServer, toolCalls));
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
            .RespondWithText(kAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        HttpClient httpClient = app.GetTestClient();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        string interruptId = GetInterruptId(await HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kQuestion));
        JsonObject resumeRun = CreateResumeRun(interruptId, true, kOtherValue);
        resumeRun["resume"]!.AsArray().Add(CreateResumeEntry(kForgedInterruptId, true, kOtherValue));

        using HttpResponseMessage response = await HarnessTestServer.PostRun(httpClient, accessToken, resumeRun.ToJsonString());
        await HarnessTestServer.ReadEventsAsync(response);

        Assert.That(toolCalls.StoredValues, Is.EqualTo(kApprovedValues));
    }

    /// <summary>
    /// Verifies that a run declaring client tools fails with an error event without asking the model.
    /// </summary>
    [Test]
    public async Task Run_WithClientTools_FailsWithoutAskingModel()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        JsonObject run = HarnessTestServer.CreateRunInput(kThreadId, kQuestion);
        run["tools"] = new JsonArray(new JsonObject { ["name"] = kClientTool, ["description"] = kClientTool, ["parameters"] = new JsonObject { ["type"] = "object" } });

        using HttpResponseMessage response = await HarnessTestServer.PostRun(app.GetTestClient(), McpTestServer.CreateMiddlewareToken(Roles.Reporter), run.ToJsonString());

        Assert.That(HarnessTestServer.GetEventTypes(await HarnessTestServer.ReadEventsAsync(response)), Does.Contain(HarnessTestServer.kRunError));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that a run naming an MCP prompt in its forwarded properties sends the prompt of the MCP server as the
    /// user's turn.
    /// </summary>
    [Test]
    public async Task Run_WithPrompt_SendsPromptOfMcpServerAsUserTurn()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        JsonObject run = HarnessTestServer.CreateRunInput(kThreadId);
        Dictionary<string, string> arguments = new() { [HarnessTestSetup.kReasonArgument] = kReason };
        run["forwardedProps"] = JsonNode.Parse(HarnessTestSetup.CreatePromptProperties(HarnessTestSetup.kAccessRequestPrompt, arguments).GetRawText());

        using HttpResponseMessage response = await HarnessTestServer.PostRun(app.GetTestClient(), McpTestServer.CreateMiddlewareToken(Roles.Requester), run.ToJsonString());

        Assert.That(HarnessTestServer.GetText(await HarnessTestServer.ReadEventsAsync(response)), Is.EqualTo(kAnswer));
        Assert.That(chatClient.ReceivedMessages.Single().Single().Text, Does.Contain(kReason));
    }

    /// <summary>
    /// Verifies that a user may run on another thread while a run of theirs is running, as threads share no state.
    /// </summary>
    [Test]
    public async Task Run_OnOtherThreadWhileRunOfSameUserIsRunning_IsAccepted()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        BlockingAnswer blockingAnswer = new();
        ScriptedChatClient chatClient = new ScriptedChatClient().Respond(blockingAnswer.AnswerAsync).RespondWithText(kNextAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        HttpClient httpClient = app.GetTestClient();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        Task<List<JsonElement>> runningTurn = HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kQuestion);
        await blockingAnswer.Started;

        List<JsonElement> otherThreadEvents = await HarnessTestServer.RunAsync(httpClient, accessToken, kOtherThreadId, kNextQuestion);
        blockingAnswer.Release(kAnswer);

        Assert.That(HarnessTestServer.GetText(otherThreadEvents), Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestServer.GetText(await runningTurn), Is.EqualTo(kAnswer));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kSecondTurnOnly));
    }

    /// <summary>
    /// Verifies that a thread runs one run at a time: a second run on it while the first is running is rejected right
    /// away without affecting the running run, while another user's run on the same thread id is accepted. Once the
    /// first run has been read, the next run on the thread is accepted and continues the conversation.
    /// </summary>
    [Test]
    public async Task Run_OnThreadWithRunningRun_IsRejectedWith409()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        BlockingAnswer blockingAnswer = new();
        ScriptedChatClient chatClient = new ScriptedChatClient().Respond(blockingAnswer.AnswerAsync).RespondWithText(kNextAnswer).RespondWithText(kNextAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        HttpClient httpClient = app.GetTestClient();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        Task<List<JsonElement>> runningTurn = HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kQuestion);
        await blockingAnswer.Started;

        using HttpResponseMessage rejected = await HarnessTestServer.PostRun(httpClient, accessToken, HarnessTestServer.CreateRunBody(kThreadId, kNextQuestion));
        List<JsonElement> otherUserEvents = await HarnessTestServer.RunAsync(httpClient, McpTestServer.CreateUserToken(kOtherUserName, Roles.Reporter),
            kThreadId, kNextQuestion);
        blockingAnswer.Release(kAnswer);
        List<JsonElement> firstEvents = await runningTurn;
        List<JsonElement> nextEvents = await HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kNextQuestion);

        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(HarnessTestServer.GetText(otherUserEvents), Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestServer.GetText(firstEvents), Is.EqualTo(kAnswer));
        Assert.That(HarnessTestServer.GetText(nextEvents), Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kFirstAndSecondTurn));
    }

    /// <summary>
    /// Verifies that a failed run releases its thread: the next run on it is accepted.
    /// </summary>
    [Test]
    public async Task Run_AfterFailedRunOnThread_IsAccepted()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient()
            .Respond((_, _, _) => Task.FromException<ChatResponse>(new InvalidOperationException(kProviderFailure)))
            .RespondWithText(kNextAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        HttpClient httpClient = app.GetTestClient();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);

        List<JsonElement> failedEvents = await HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kQuestion);
        List<JsonElement> nextEvents = await HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kNextQuestion);

        Assert.That(HarnessTestServer.GetEventTypes(failedEvents), Does.Contain(HarnessTestServer.kRunError));
        Assert.That(HarnessTestServer.GetText(nextEvents), Is.EqualTo(kNextAnswer));
    }

    /// <summary>
    /// Verifies that a user runs at most the configured number of runs at a time: one more run, even on another thread,
    /// is rejected right away with a retry hint and does not affect the running run, while another user's run is
    /// accepted.
    /// </summary>
    [Test]
    public async Task Run_BeyondConcurrentRunsOfUser_IsRejectedWith429()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        BlockingAnswer blockingAnswer = new();
        ScriptedChatClient chatClient = new ScriptedChatClient().Respond(blockingAnswer.AnswerAsync).RespondWithText(kNextAnswer);
        HarnessSettings settings = new() { McpEndpoint = HarnessTestSetup.kTestMcpEndpoint, MaxConcurrentRunsPerUser = kSingleConcurrentRun };
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient, settings);
        HttpClient httpClient = app.GetTestClient();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        Task<List<JsonElement>> runningTurn = HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kQuestion);
        await blockingAnswer.Started;

        using HttpResponseMessage rejected = await HarnessTestServer.PostRun(httpClient, accessToken, HarnessTestServer.CreateRunBody(kOtherThreadId, kNextQuestion));
        List<JsonElement> otherUserEvents = await HarnessTestServer.RunAsync(httpClient, McpTestServer.CreateUserToken(kOtherUserName, Roles.Reporter),
            kThreadId, kNextQuestion);
        blockingAnswer.Release(kAnswer);

        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(rejected.Headers.RetryAfter, Is.Not.Null);
        Assert.That(HarnessTestServer.GetText(otherUserEvents), Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestServer.GetText(await runningTurn), Is.EqualTo(kAnswer));
    }

    /// <summary>
    /// Verifies that the runs of a user beyond the budget per minute are rejected without asking the model.
    /// </summary>
    [Test]
    public async Task Run_BeyondRunsPerMinute_IsRejectedWith429()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        HarnessSettings settings = new() { McpEndpoint = HarnessTestSetup.kTestMcpEndpoint, RunsPerMinute = kSingleRunPerMinute };
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient, settings);
        HttpClient httpClient = app.GetTestClient();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        await HarnessTestServer.RunAsync(httpClient, accessToken, kThreadId, kQuestion);

        using HttpResponseMessage response = await HarnessTestServer.PostRun(httpClient, accessToken, HarnessTestServer.CreateRunBody(kThreadId, kNextQuestion));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(chatClient.ReceivedMessages, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Verifies that a request body larger than the maximum size is rejected with 413 (on Kestrel, which enforces the limit).
    /// </summary>
    [Test]
    public async Task Run_WithBodyOverLimit_IsRejectedWith413()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        await using WebApplication app = await HarnessTestServer.StartOnLoopback(mcpApp, chatClient);
        using HttpClient httpClient = new() { BaseAddress = McpTestServer.GetBaseAddress(app) };
        string oversizedText = new('x', (int)HarnessRateLimiting.kMaxRequestBodyBytes);

        using HttpResponseMessage response = await HarnessTestServer.PostRun(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter),
            HarnessTestServer.CreateRunBody(kThreadId, oversizedText));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that the server starts without a configured AI provider, with the agent factory Program creates from the
    /// settings: the factory is only created by a run, which then fails with 500 as long as the factory throws, while
    /// a request without a token is still rejected with 401 before that.
    /// </summary>
    [Test]
    public async Task Run_WithoutConfiguredProvider_FailsAfterServerStarted()
    {
        HarnessSettings unconfiguredSettings = new();
        int factoryCalls = 0;
        await using WebApplication app = await HarnessTestServer.StartOnLoopback(unconfiguredSettings, _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return new FwoAgentFactory(unconfiguredSettings.Provider, unconfiguredSettings.Agent);
        });
        using HttpClient httpClient = new() { BaseAddress = McpTestServer.GetBaseAddress(app) };
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        int factoryCallsAtStart = factoryCalls;

        using HttpResponseMessage withoutToken = await HarnessTestServer.PostRun(httpClient, null, HarnessTestServer.CreateRunBody(kThreadId, kQuestion));
        using HttpResponseMessage firstRun = await HarnessTestServer.PostRun(httpClient, accessToken, HarnessTestServer.CreateRunBody(kThreadId, kQuestion));
        using HttpResponseMessage secondRun = await HarnessTestServer.PostRun(httpClient, accessToken, HarnessTestServer.CreateRunBody(kThreadId, kQuestion));

        Assert.That(factoryCallsAtStart, Is.Zero);
        Assert.That(withoutToken.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(firstRun.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(secondRun.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(factoryCalls, Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies that the AG-UI endpoint carries the run policy and the body size limit.
    /// </summary>
    [Test]
    public async Task AgUiEndpoint_UsesRunPolicyAndBodySizeLimit()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, new ScriptedChatClient());

        Endpoint endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(dataSource => dataSource.Endpoints).Single();

        Assert.That(endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName, Is.EqualTo(HarnessRateLimiting.kRunPolicy));
        Assert.That(endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize, Is.EqualTo(HarnessRateLimiting.kMaxRequestBodyBytes));
    }

    /// <summary>
    /// Verifies that the run partition admits the given number of runs of a user at a time, and that users are told
    /// apart by their directory identity.
    /// </summary>
    [Test]
    public void RunPartition_AdmitsConcurrentRunsOfUserUpToLimit()
    {
        DefaultHttpContext context = new() { User = CreateUser(new Claim(JwtClaimTypes.kUserDn, McpTestServer.UserDn(McpTestServer.kUserName))) };
        RateLimitPartition<string> partition = HarnessRateLimiting.CreateRunPartition(context, kTwoConcurrentRuns);
        using RateLimiter limiter = partition.Factory(partition.PartitionKey);

        using RateLimitLease first = limiter.AttemptAcquire();
        using RateLimitLease second = limiter.AttemptAcquire();
        using RateLimitLease third = limiter.AttemptAcquire();

        Assert.That(first.IsAcquired, Is.True);
        Assert.That(second.IsAcquired, Is.True);
        Assert.That(third.IsAcquired, Is.False);
        Assert.That(partition.PartitionKey, Does.Contain(McpTestServer.UserDn(McpTestServer.kUserName)));
    }

    /// <summary>
    /// Verifies that the partition falls back to the user name, and to one shared partition without either.
    /// </summary>
    [Test]
    public void GetPartitionKey_WithoutDn_UsesNameOrSharedPartition()
    {
        ClaimsPrincipal namedUser = new(new ClaimsIdentity(new List<Claim> { new(ClaimTypes.Name, McpTestServer.kUserName) }, "test"));
        ClaimsPrincipal anonymousUser = new(new ClaimsIdentity());

        Assert.That(HarnessRateLimiting.GetPartitionKey(namedUser), Does.Contain(McpTestServer.kUserName));
        Assert.That(HarnessRateLimiting.GetPartitionKey(anonymousUser), Is.EqualTo(HarnessRateLimiting.GetPartitionKey(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    /// <summary>
    /// Verifies that no agent is selected for a request without bearer token, i.e. the endpoint answers 404.
    /// </summary>
    [Test]
    public async Task SelectUserAgent_WithoutBearerToken_ReturnsNull()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(mcpApp, new ScriptedChatClient());
        DefaultHttpContext basicContext = new();
        basicContext.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";

        Assert.That(HarnessServerSetup.SelectUserAgent(new DefaultHttpContext(), factory, new HarnessSettings()), Is.Null);
        Assert.That(HarnessServerSetup.SelectUserAgent(basicContext, factory, new HarnessSettings()), Is.Null);
    }

    /// <summary>
    /// Returns the id of the single interrupt the run finished with.
    /// </summary>
    private static string GetInterruptId(List<JsonElement> events)
    {
        JsonElement runFinished = events.Single(aguiEvent => aguiEvent.GetProperty("type").GetString() == HarnessTestServer.kRunFinished);
        JsonElement interrupt = runFinished.GetProperty("outcome").GetProperty("interrupts").EnumerateArray().Single();
        return interrupt.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Creates a run on the test thread with a resume entry answering the given interrupt for a call storing the given value.
    /// </summary>
    private static JsonObject CreateResumeRun(string interruptId, bool approved, string value)
    {
        JsonObject run = HarnessTestServer.CreateRunInput(kThreadId);
        run["resume"] = new JsonArray(CreateResumeEntry(interruptId, approved, value));
        return run;
    }

    /// <summary>
    /// Creates a resume entry answering the given interrupt for a call of the test tool storing the given value.
    /// </summary>
    private static JsonObject CreateResumeEntry(string interruptId, bool approved, string value)
    {
        JsonObject toolCall = new()
        {
            ["callId"] = kCallId,
            ["name"] = McpTestWriteTools.kStoreValueTool,
            ["arguments"] = new JsonObject { [McpTestWriteTools.kValueArgument] = value }
        };
        return new JsonObject { ["interruptId"] = interruptId, ["payload"] = new JsonObject { ["approved"] = approved, ["toolCall"] = toolCall } };
    }

    /// <summary>
    /// Creates the AG-UI messages of a conversation alternating between user and assistant, starting with the user.
    /// </summary>
    private static JsonArray CreateConversation(params string[] texts)
    {
        JsonArray messages = new();
        for (int index = 0; index < texts.Length; index++)
        {
            string role = index % 2 == 0 ? "user" : "assistant";
            messages.Add(new JsonObject { ["id"] = $"conversation-{index}", ["role"] = role, ["content"] = texts[index] });
        }
        return messages;
    }

    /// <summary>
    /// Creates the arguments of a call of the test tool storing the given value.
    /// </summary>
    private static Dictionary<string, object?> CreateArguments(string value)
    {
        return new Dictionary<string, object?> { [McpTestWriteTools.kValueArgument] = value };
    }

    /// <summary>
    /// Creates an authenticated user with the given claims.
    /// </summary>
    private static ClaimsPrincipal CreateUser(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    /// <summary>
    /// Creates a valid token of a user with the given role but without the directory identity of the user.
    /// </summary>
    private static string CreateTokenWithoutDn(string role)
    {
        ClaimsIdentity subject = new(new List<Claim> { new(ClaimTypes.Name, McpTestServer.kUserName), new(ClaimTypes.Role, role) });
        JwtSecurityTokenHandler tokenHandler = new();
        JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken(
            issuer: JwtConstants.Issuer,
            audience: JwtConstants.Audience,
            subject: subject,
            expires: DateTime.UtcNow.AddMinutes(kTokenLifetimeMinutes),
            signingCredentials: new SigningCredentials(McpTestServer.PrivateKey, SecurityAlgorithms.RsaSha256));
        return tokenHandler.WriteToken(token);
    }
}
