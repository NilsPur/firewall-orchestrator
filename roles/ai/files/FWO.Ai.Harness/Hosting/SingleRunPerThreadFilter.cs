using AGUI.Abstractions;
using FWO.Logging;

namespace FWO.Ai.Harness.Hosting;

/// <summary>
/// Filter of the AG-UI endpoint that lets each thread of a user run one run at a time; a run on a thread whose previous
/// run is still running gets 409. The thread stays locked from loading its session until the session is stored after
/// the run. Two runs on one thread would otherwise both start from the same stored session, so one run's history would
/// get lost and an approved tool call could run twice. Runs on different threads share no state and run at the same
/// time, up to the limit of the run policy (see <see cref="HarnessRateLimiting.kRunPolicy"/>).
/// </summary>
internal sealed class SingleRunPerThreadFilter : IEndpointFilter
{
    /// <summary>Format of the thread ids given to runs without one, like the AG-UI hosting does.</summary>
    private const string kThreadIdFormat = "N";

    private readonly HashSet<ThreadKey> runningThreads = new();
    private readonly object runningThreadsLock = new();

    /// <summary>
    /// Runs the AG-UI endpoint unless another run of the calling user is running on the thread of the input, and keeps
    /// the thread locked until the result of the endpoint has been executed: the AG-UI hosting runs the agent and stores
    /// the session while it streams the result. A run without a thread id gets a new one here rather than in the AG-UI
    /// hosting, so that its thread is locked as well, as the client learns the id from RUN_STARTED during the run.
    /// </summary>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        RunAgentInput? input = context.Arguments.OfType<RunAgentInput>().FirstOrDefault();
        if (input == null)
        {
            // the AG-UI endpoint answers 400 without loading a session
            return await next(context);
        }
        if (string.IsNullOrWhiteSpace(input.ThreadId))
        {
            input.ThreadId = Guid.NewGuid().ToString(kThreadIdFormat);
        }
        ThreadKey thread = new(HarnessRateLimiting.GetPartitionKey(context.HttpContext.User), input.ThreadId);
        if (!TryStartRun(thread))
        {
            Log.WriteWarning("Thread Busy", $"Rejected AG-UI run for {thread.Owner} on a thread with a running run.");
            return TypedResults.Conflict();
        }
        object? result;
        try
        {
            result = await next(context);
        }
        catch
        {
            EndRun(thread);
            throw;
        }
        if (result is IResult runResult)
        {
            return new ThreadRunResult(runResult, () => EndRun(thread));
        }
        EndRun(thread);
        return result;
    }

    /// <summary>
    /// Locks the thread for a run; returns false if a run is already running on it.
    /// </summary>
    private bool TryStartRun(ThreadKey thread)
    {
        lock (runningThreadsLock)
        {
            return runningThreads.Add(thread);
        }
    }

    /// <summary>
    /// Releases the thread after its run.
    /// </summary>
    private void EndRun(ThreadKey thread)
    {
        lock (runningThreadsLock)
        {
            runningThreads.Remove(thread);
        }
    }

    /// <summary>
    /// Thread of a user: the partition of the user (see <see cref="HarnessRateLimiting.GetPartitionKey"/>) and the
    /// AG-UI thread id.
    /// </summary>
    private readonly record struct ThreadKey(string Owner, string ThreadId);

    /// <summary>
    /// Result of the AG-UI endpoint that releases the thread once it has been executed, also if executing it fails.
    /// The thread is released before the response ends, so the client may start the next run as soon as it has read
    /// the result.
    /// </summary>
    private sealed class ThreadRunResult(IResult runResult, Action endRun) : IResult
    {
        /// <summary>
        /// Executes the result of the run, i.e. runs the agent and streams its events, then releases the thread.
        /// </summary>
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            try
            {
                await runResult.ExecuteAsync(httpContext);
            }
            finally
            {
                endRun();
            }
        }
    }
}
