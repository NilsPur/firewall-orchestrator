using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using FWO.Basics;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the bounded session store of the AG-UI threads: sessions are restored with their history, kept apart by
/// owner, dropped when idle for too long, and limited per owner.
/// </summary>
[TestFixture]
internal class HarnessSessionStoreTest
{
    private const string kIsolationPartition = "isolation";
    private const string kFirstUser = "uid=first,ou=users,dc=test";
    private const string kSecondUser = "uid=second,ou=users,dc=test";
    private const string kFirstThread = "thread-1";
    private const string kSecondThread = "thread-2";
    private const string kThirdThread = "thread-3";
    private const string kQuestion = "Which roles do I have?";
    private const string kAnswer = "You are a reporter.";
    private const int kMaxSessionsPerOwner = 2;

    private static readonly TimeSpan kIdleTimeout = TimeSpan.FromHours(1);
    private static readonly TimeSpan kShortWhile = TimeSpan.FromMinutes(1);
    private static readonly List<string> kQuestionAndAnswer = [kQuestion, kAnswer];

    /// <summary>
    /// Verifies that a stored session is restored with its history, and that an unknown thread has no session.
    /// </summary>
    [Test]
    public async Task GetSessionAsync_AfterSave_RestoresSessionWithHistory()
    {
        await using WebApplication app = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient().RespondWithText(kAnswer));
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();
        await agent.RunAsync(kQuestion, session);
        BoundedAgentSessionStore store = new(kMaxSessionsPerOwner, kIdleTimeout, new ManualTimeProvider());

        await store.SaveSessionAsync(agent, CreateKey(kFirstThread, kFirstUser), session);
        AgentSession? restoredSession = await store.GetSessionAsync(agent, CreateKey(kFirstThread, kFirstUser));

        Assert.That(restoredSession, Is.Not.Null);
        Assert.That(HarnessTestSetup.GetTexts(factory.GetHistory(restoredSession!)), Is.EqualTo(kQuestionAndAnswer));
        Assert.That(await store.GetSessionAsync(agent, CreateKey(kSecondThread, kFirstUser)), Is.Null);
    }

    /// <summary>
    /// Verifies that the session of a thread is kept apart by owner: the same thread id of another user has no session.
    /// </summary>
    [Test]
    public async Task GetSessionAsync_OfOtherOwner_ReturnsNull()
    {
        await using WebApplication app = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient());
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        BoundedAgentSessionStore store = new(kMaxSessionsPerOwner, kIdleTimeout, new ManualTimeProvider());

        await store.SaveSessionAsync(agent, CreateKey(kFirstThread, kFirstUser), await agent.CreateSessionAsync());

        Assert.That(await store.GetSessionAsync(agent, CreateKey(kFirstThread, kSecondUser)), Is.Null);
        Assert.That(await store.GetSessionAsync(agent, new AgentSessionStoreKey(kFirstThread)), Is.Null);
    }

    /// <summary>
    /// Verifies that a session idle for longer than the timeout is dropped, while one used within it is kept, as each
    /// read counts as use.
    /// </summary>
    [Test]
    public async Task GetSessionAsync_AfterIdleTimeout_ReturnsNull()
    {
        await using WebApplication app = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient());
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        ManualTimeProvider clock = new();
        BoundedAgentSessionStore store = new(kMaxSessionsPerOwner, kIdleTimeout, clock);
        await store.SaveSessionAsync(agent, CreateKey(kFirstThread, kFirstUser), await agent.CreateSessionAsync());
        await store.SaveSessionAsync(agent, CreateKey(kSecondThread, kFirstUser), await agent.CreateSessionAsync());

        clock.Advance(kIdleTimeout - kShortWhile);
        AgentSession? usedSession = await store.GetSessionAsync(agent, CreateKey(kSecondThread, kFirstUser));
        clock.Advance(kShortWhile + kShortWhile);

        Assert.That(usedSession, Is.Not.Null);
        Assert.That(await store.GetSessionAsync(agent, CreateKey(kFirstThread, kFirstUser)), Is.Null);
        Assert.That(await store.GetSessionAsync(agent, CreateKey(kSecondThread, kFirstUser)), Is.Not.Null);
        Assert.That(store.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that an owner keeps at most the maximum number of sessions: storing one more drops the least recently
    /// used one of that owner, not the sessions of other owners.
    /// </summary>
    [Test]
    public async Task SaveSessionAsync_BeyondMaximum_DropsLeastRecentlyUsedOfOwner()
    {
        await using WebApplication app = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient());
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        ManualTimeProvider clock = new();
        BoundedAgentSessionStore store = new(kMaxSessionsPerOwner, kIdleTimeout, clock);
        await store.SaveSessionAsync(agent, CreateKey(kFirstThread, kSecondUser), await agent.CreateSessionAsync());
        await store.SaveSessionAsync(agent, CreateKey(kFirstThread, kFirstUser), await agent.CreateSessionAsync());
        clock.Advance(kShortWhile);
        await store.SaveSessionAsync(agent, CreateKey(kSecondThread, kFirstUser), await agent.CreateSessionAsync());
        clock.Advance(kShortWhile);
        await store.GetSessionAsync(agent, CreateKey(kFirstThread, kFirstUser));

        await store.SaveSessionAsync(agent, CreateKey(kThirdThread, kFirstUser), await agent.CreateSessionAsync());

        Assert.That(await store.GetSessionAsync(agent, CreateKey(kSecondThread, kFirstUser)), Is.Null);
        Assert.That(await store.GetSessionAsync(agent, CreateKey(kFirstThread, kFirstUser)), Is.Not.Null);
        Assert.That(await store.GetSessionAsync(agent, CreateKey(kThirdThread, kFirstUser)), Is.Not.Null);
        Assert.That(await store.GetSessionAsync(agent, CreateKey(kFirstThread, kSecondUser)), Is.Not.Null);
    }

    /// <summary>
    /// Verifies that storing a session of a thread again replaces it instead of counting as another session, also when
    /// all sessions were used at the same time.
    /// </summary>
    [Test]
    public async Task SaveSessionAsync_OfSameThread_ReplacesSession()
    {
        await using WebApplication app = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient());
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        BoundedAgentSessionStore store = new(kMaxSessionsPerOwner, kIdleTimeout, new ManualTimeProvider());
        await store.SaveSessionAsync(agent, CreateKey(kFirstThread, kFirstUser), await agent.CreateSessionAsync());
        await store.SaveSessionAsync(agent, CreateKey(kSecondThread, kFirstUser), await agent.CreateSessionAsync());

        await store.SaveSessionAsync(agent, CreateKey(kSecondThread, kFirstUser), await agent.CreateSessionAsync());
        await store.SaveSessionAsync(agent, CreateKey(kThirdThread, kFirstUser), await agent.CreateSessionAsync());

        Assert.That(store.Count, Is.EqualTo(kMaxSessionsPerOwner));
        Assert.That(await store.GetSessionAsync(agent, CreateKey(kThirdThread, kFirstUser)), Is.Not.Null);
    }

    /// <summary>
    /// Verifies that a deleted session is gone, and that deleting an unknown session does nothing.
    /// </summary>
    [Test]
    public async Task DeleteSessionAsync_RemovesSession()
    {
        await using WebApplication app = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient());
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        BoundedAgentSessionStore store = new(kMaxSessionsPerOwner, kIdleTimeout, new ManualTimeProvider());
        await store.SaveSessionAsync(agent, CreateKey(kFirstThread, kFirstUser), await agent.CreateSessionAsync());

        await store.DeleteSessionAsync(agent, CreateKey(kFirstThread, kFirstUser));
        await store.DeleteSessionAsync(agent, CreateKey(kSecondThread, kFirstUser));

        Assert.That(store.Count, Is.Zero);
    }

    /// <summary>
    /// Verifies that invalid limits are refused.
    /// </summary>
    [Test]
    public void Ctor_WithInvalidLimits_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BoundedAgentSessionStore(0, kIdleTimeout, TimeProvider.System));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BoundedAgentSessionStore(kMaxSessionsPerOwner, TimeSpan.Zero, TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() => _ = new BoundedAgentSessionStore(kMaxSessionsPerOwner, kIdleTimeout, null!));
    }

    /// <summary>
    /// Creates the key of a thread of a user, as the isolation of the AG-UI hosting does.
    /// </summary>
    private static AgentSessionStoreKey CreateKey(string threadId, string userDn)
    {
        return new AgentSessionStoreKey(threadId).WithPartition(kIsolationPartition, userDn);
    }

    /// <summary>
    /// Clock that only moves on when told to.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UnixEpoch;

        /// <summary>
        /// Moves the clock on.
        /// </summary>
        internal void Advance(TimeSpan time)
        {
            now += time;
        }

        /// <summary>
        /// Returns the current time of the clock.
        /// </summary>
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
