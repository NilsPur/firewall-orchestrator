using AGUI.Abstractions;
using FWO.Ai.Harness.Hosting;
using FWO.Basics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the filter of the AG-UI endpoint that lets each thread of a user run one run at a time: a run on a thread
/// with a running run is rejected, the thread is released once the result of its run has been executed or the run has
/// failed, and a run without a thread id gets a thread of its own that is locked as well.
/// </summary>
[TestFixture]
internal class HarnessSingleRunPerThreadFilterTest
{
    private const string kFirstUserDn = "uid=first,ou=users,dc=test";
    private const string kSecondUserDn = "uid=second,ou=users,dc=test";
    private const string kThreadId = "thread-1";
    private const string kOtherThreadId = "thread-2";
    private const string kFailure = "endpoint failed";
    private const string kPlainAnswer = "plain answer";
    private const int kCallsOfPassedRuns = 4;

    /// <summary>
    /// Verifies that a run on a thread with a running run gets 409 without reaching the endpoint, while the run of
    /// another user on the same thread id and a run of the same user on another thread pass, and that the thread is
    /// released once the result of its run has been executed.
    /// </summary>
    [Test]
    public async Task InvokeAsync_OnThreadWithRunningRun_ReturnsConflictUntilResultExecuted()
    {
        SingleRunPerThreadFilter filter = new();
        TestResult runResult = new();
        TestEndpoint endpoint = new(() => runResult);
        IResult running = await StartRunAsync(filter, CreateContext(kFirstUserDn, kThreadId), endpoint);

        object? rejected = await filter.InvokeAsync(CreateContext(kFirstUserDn, kThreadId), endpoint.InvokeAsync);
        await filter.InvokeAsync(CreateContext(kSecondUserDn, kThreadId), endpoint.InvokeAsync);
        await filter.InvokeAsync(CreateContext(kFirstUserDn, kOtherThreadId), endpoint.InvokeAsync);
        await running.ExecuteAsync(new DefaultHttpContext());
        object? next = await filter.InvokeAsync(CreateContext(kFirstUserDn, kThreadId), endpoint.InvokeAsync);

        Assert.That(rejected, Is.InstanceOf<Conflict>());
        Assert.That(next, Is.Not.InstanceOf<Conflict>());
        Assert.That(endpoint.Calls, Is.EqualTo(kCallsOfPassedRuns));
        Assert.That(runResult.Executions, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that a run without a thread id gets a new thread id, which is locked while the run is running, and
    /// that each such run gets a thread of its own.
    /// </summary>
    [Test]
    public async Task InvokeAsync_WithoutThreadId_GivesRunLockedThreadOfItsOwn()
    {
        SingleRunPerThreadFilter filter = new();
        TestEndpoint endpoint = new(() => new TestResult());
        RunAgentInput firstInput = new() { ThreadId = "" };
        RunAgentInput secondInput = new() { ThreadId = " " };

        await StartRunAsync(filter, CreateContext(kFirstUserDn, firstInput), endpoint);
        await StartRunAsync(filter, CreateContext(kFirstUserDn, secondInput), endpoint);
        object? onFirstThread = await filter.InvokeAsync(CreateContext(kFirstUserDn, firstInput.ThreadId), endpoint.InvokeAsync);

        Assert.That(firstInput.ThreadId, Is.Not.Empty.And.Not.EqualTo(secondInput.ThreadId));
        Assert.That(secondInput.ThreadId, Is.Not.Empty);
        Assert.That(onFirstThread, Is.InstanceOf<Conflict>());
    }

    /// <summary>
    /// Verifies that a run whose endpoint fails releases its thread.
    /// </summary>
    [Test]
    public async Task InvokeAsync_WhenEndpointFails_ReleasesThread()
    {
        SingleRunPerThreadFilter filter = new();
        TestEndpoint failingEndpoint = new(() => throw new InvalidOperationException(kFailure));
        TestEndpoint endpoint = new(() => new TestResult());

        Assert.ThrowsAsync<InvalidOperationException>(async () => await filter.InvokeAsync(CreateContext(kFirstUserDn, kThreadId), failingEndpoint.InvokeAsync));
        object? next = await filter.InvokeAsync(CreateContext(kFirstUserDn, kThreadId), endpoint.InvokeAsync);

        Assert.That(next, Is.Not.InstanceOf<Conflict>());
        Assert.That(endpoint.Calls, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that a run whose result fails while it is executed releases its thread.
    /// </summary>
    [Test]
    public async Task InvokeAsync_WhenResultFails_ReleasesThread()
    {
        SingleRunPerThreadFilter filter = new();
        TestEndpoint failingEndpoint = new(() => new TestResult(new InvalidOperationException(kFailure)));
        TestEndpoint endpoint = new(() => new TestResult());
        IResult running = await StartRunAsync(filter, CreateContext(kFirstUserDn, kThreadId), failingEndpoint);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await running.ExecuteAsync(new DefaultHttpContext()));
        object? next = await filter.InvokeAsync(CreateContext(kFirstUserDn, kThreadId), endpoint.InvokeAsync);

        Assert.That(next, Is.Not.InstanceOf<Conflict>());
        Assert.That(endpoint.Calls, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that an answer of the endpoint that is not a result is returned as it is and releases the thread.
    /// </summary>
    [Test]
    public async Task InvokeAsync_WithAnswerOtherThanResult_ReturnsAnswerAndReleasesThread()
    {
        SingleRunPerThreadFilter filter = new();
        TestEndpoint endpoint = new(() => kPlainAnswer);

        object? answer = await filter.InvokeAsync(CreateContext(kFirstUserDn, kThreadId), endpoint.InvokeAsync);
        object? next = await filter.InvokeAsync(CreateContext(kFirstUserDn, kThreadId), endpoint.InvokeAsync);

        Assert.That(answer, Is.EqualTo(kPlainAnswer));
        Assert.That(next, Is.EqualTo(kPlainAnswer));
    }

    /// <summary>
    /// Verifies that a request without AG-UI input is passed on to the endpoint, which rejects it, and that its
    /// result is returned as it is.
    /// </summary>
    [Test]
    public async Task InvokeAsync_WithoutInput_PassesRequestOn()
    {
        SingleRunPerThreadFilter filter = new();
        TestResult badRequest = new();
        TestEndpoint endpoint = new(() => badRequest);

        object? result = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(CreateHttpContext(kFirstUserDn)), endpoint.InvokeAsync);

        Assert.That(result, Is.SameAs(badRequest));
        Assert.That(endpoint.Calls, Is.EqualTo(1));
    }

    /// <summary>
    /// Starts a run through the filter and returns the result of the endpoint that the filter wrapped.
    /// </summary>
    private static async Task<IResult> StartRunAsync(SingleRunPerThreadFilter filter, EndpointFilterInvocationContext context, TestEndpoint endpoint)
    {
        object? result = await filter.InvokeAsync(context, endpoint.InvokeAsync);
        Assert.That(result, Is.InstanceOf<IResult>().And.Not.InstanceOf<Conflict>());
        return (IResult)result!;
    }

    /// <summary>
    /// Creates the filter context of a run of the given user on the given thread.
    /// </summary>
    private static EndpointFilterInvocationContext CreateContext(string userDn, string threadId)
    {
        return CreateContext(userDn, new RunAgentInput { ThreadId = threadId });
    }

    /// <summary>
    /// Creates the filter context of a run of the given user with the given input, among the other arguments of the
    /// AG-UI endpoint.
    /// </summary>
    private static EndpointFilterInvocationContext CreateContext(string userDn, RunAgentInput input)
    {
        return new DefaultEndpointFilterInvocationContext(CreateHttpContext(userDn), input, kPlainAnswer);
    }

    /// <summary>
    /// Creates the context of a request of the given user.
    /// </summary>
    private static DefaultHttpContext CreateHttpContext(string userDn)
    {
        ClaimsIdentity identity = new(new List<Claim> { new(JwtClaimTypes.kUserDn, userDn) }, "test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    /// <summary>
    /// Endpoint standing in for the AG-UI endpoint: counts its calls and answers as told.
    /// </summary>
    private sealed class TestEndpoint(Func<object?> answer)
    {
        /// <summary>Number of calls of the endpoint.</summary>
        internal int Calls { get; private set; }

        /// <summary>
        /// Counts the call and answers as told.
        /// </summary>
        internal ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context)
        {
            Calls++;
            return ValueTask.FromResult(answer());
        }
    }

    /// <summary>
    /// Result standing in for the event stream of a run: counts its executions and fails as told.
    /// </summary>
    private sealed class TestResult(Exception? failure = null) : IResult
    {
        /// <summary>Number of executions of the result.</summary>
        internal int Executions { get; private set; }

        /// <summary>
        /// Counts the execution and fails as told.
        /// </summary>
        public Task ExecuteAsync(HttpContext httpContext)
        {
            Executions++;
            return failure == null ? Task.CompletedTask : Task.FromException(failure);
        }
    }
}
