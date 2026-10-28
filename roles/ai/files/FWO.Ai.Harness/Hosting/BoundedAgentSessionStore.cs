using Microsoft.Agents.AI;
using System.Text.Json;

namespace FWO.Ai.Harness.Hosting;

/// <summary>
/// Keeps the sessions of the AG-UI threads in memory, bounded so that a client cannot exhaust the memory by starting
/// new threads: a session not used for the idle timeout is dropped, and of the sessions of one owner only the most
/// recently used ones are kept. The owner of a session is the agent together with all partitions of its key, i.e. the
/// isolation partition of the user that the AG-UI hosting adds. The InMemoryAgentSessionStore of the Agent Framework
/// instead keeps every session for the lifetime of the process.
/// The sessions hold the tool results, i.e. firewall data, and are lost when the harness server restarts.
/// </summary>
internal sealed class BoundedAgentSessionStore : AgentSessionStore
{
    private readonly object storeLock = new();
    private readonly Dictionary<(string AgentName, AgentSessionStoreKey Key), StoredSession> sessions = new();
    private readonly int maxSessionsPerOwner;
    private readonly TimeSpan idleTimeout;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Creates the empty store.
    /// </summary>
    /// <param name="maxSessionsPerOwner">Sessions kept per owner; must be positive</param>
    /// <param name="idleTimeout">Time after which an unused session is dropped; must be positive</param>
    /// <param name="timeProvider">Clock of the idle timeout</param>
    internal BoundedAgentSessionStore(int maxSessionsPerOwner, TimeSpan idleTimeout, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSessionsPerOwner);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.maxSessionsPerOwner = maxSessionsPerOwner;
        this.idleTimeout = idleTimeout;
        this.timeProvider = timeProvider;
    }

    /// <summary>Number of sessions kept, including idle ones not dropped yet.</summary>
    internal int Count
    {
        get
        {
            lock (storeLock)
            {
                return sessions.Count;
            }
        }
    }

    /// <summary>
    /// Stores the session and drops the least recently used session of its owner if the owner has too many.
    /// </summary>
    public override async ValueTask SaveSessionAsync(AIAgent agent, AgentSessionStoreKey key, AgentSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(session);
        JsonElement serializedSession = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
        string agentName = GetAgentName(agent);
        (string, AgentSessionStoreKey) storageKey = (agentName, key);
        AgentSessionStoreKey owner = new(agentName, key.Partitions);
        lock (storeLock)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            RemoveIdleSessions(now);
            sessions[storageKey] = new StoredSession(serializedSession, owner) { LastUsed = now };
            RemoveLeastRecentlyUsedSessions(owner, storageKey);
        }
    }

    /// <summary>
    /// Returns the stored session, or null if there is none or it has been idle for too long.
    /// </summary>
    public override async ValueTask<AgentSession?> GetSessionAsync(AIAgent agent, AgentSessionStoreKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(key);
        JsonElement? serializedSession = null;
        lock (storeLock)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            RemoveIdleSessions(now);
            if (sessions.TryGetValue((GetAgentName(agent), key), out StoredSession? storedSession))
            {
                storedSession.LastUsed = now;
                serializedSession = storedSession.SerializedSession;
            }
        }
        return serializedSession is JsonElement session
            ? await agent.DeserializeSessionAsync(session, cancellationToken: cancellationToken)
            : null;
    }

    /// <summary>
    /// Removes the stored session, if there is one.
    /// </summary>
    public override ValueTask DeleteSessionAsync(AIAgent agent, AgentSessionStoreKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(key);
        lock (storeLock)
        {
            sessions.Remove((GetAgentName(agent), key));
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Returns the name identifying the sessions of an agent; agents without a name are told apart by their id.
    /// </summary>
    private static string GetAgentName(AIAgent agent)
    {
        return string.IsNullOrWhiteSpace(agent.Name) ? agent.Id : agent.Name;
    }

    /// <summary>
    /// Drops the sessions not used for the idle timeout; to be called holding the store lock.
    /// </summary>
    private void RemoveIdleSessions(DateTimeOffset now)
    {
        List<(string, AgentSessionStoreKey)> idleKeys = sessions
            .Where(entry => now - entry.Value.LastUsed > idleTimeout)
            .Select(entry => entry.Key)
            .ToList();
        foreach ((string, AgentSessionStoreKey) idleKey in idleKeys)
        {
            sessions.Remove(idleKey);
        }
    }

    /// <summary>
    /// Drops the least recently used sessions of an owner beyond the maximum, never the session just stored;
    /// to be called holding the store lock.
    /// </summary>
    private void RemoveLeastRecentlyUsedSessions(AgentSessionStoreKey owner, (string, AgentSessionStoreKey) storedKey)
    {
        List<KeyValuePair<(string, AgentSessionStoreKey), StoredSession>> otherSessions = sessions
            .Where(entry => entry.Value.Owner.Equals(owner) && !entry.Key.Equals(storedKey))
            .OrderBy(entry => entry.Value.LastUsed)
            .ToList();
        // the stored session counts towards the maximum as well
        int surplus = otherSessions.Count + 1 - maxSessionsPerOwner;
        foreach (KeyValuePair<(string, AgentSessionStoreKey), StoredSession> surplusSession in otherSessions.Take(surplus))
        {
            sessions.Remove(surplusSession.Key);
        }
    }

    /// <summary>
    /// A stored session and its owner.
    /// </summary>
    /// <param name="SerializedSession">Session as serialized by its agent</param>
    /// <param name="Owner">The agent and the partitions of the session key</param>
    private sealed record StoredSession(JsonElement SerializedSession, AgentSessionStoreKey Owner)
    {
        /// <summary>When the session was last stored or read.</summary>
        public DateTimeOffset LastUsed { get; set; }
    }
}
