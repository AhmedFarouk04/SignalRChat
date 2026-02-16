using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace EnterpriseChat.Infrastructure.Presence;

public sealed class InMemoryPresenceService : IPresenceService
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _lastSeen = new(); // ➕ إضافة Last Seen

    public Task UserConnectedAsync(UserId userId, string connectionId)
    {
        var connections = _connections.GetOrAdd(userId.Value, _ => new HashSet<string>());
        lock (connections)
        {
            connections.Add(connectionId);
        }

        // ➕ إزالة Last Seen عند الاتصال
        _lastSeen.TryRemove(userId.Value, out _);

        return Task.CompletedTask;
    }
    public Task<int> GetUserConnectionsCountAsync(UserId userId)
    {
        if (_connections.TryGetValue(userId.Value, out var connections))
        {
            lock (connections)
            {
                return Task.FromResult(connections.Count);
            }
        }
        return Task.FromResult(0);
    }
    public Task UserDisconnectedAsync(UserId userId, string connectionId)
    {
        if (_connections.TryGetValue(userId.Value, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _connections.TryRemove(userId.Value, out _);
                    // ➕ تسجيل Last Seen عند قطع آخر اتصال
                    _lastSeen[userId.Value] = DateTime.UtcNow;
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task<bool> IsOnlineAsync(UserId userId)
        => Task.FromResult(_connections.ContainsKey(userId.Value));

    public Task<DateTime?> GetLastSeenAsync(UserId userId)
    {
        _lastSeen.TryGetValue(userId.Value, out var lastSeen);
        return Task.FromResult<DateTime?>(lastSeen);
    }
    public Task UpdateHeartbeatAsync(UserId userId)
    {
        // InMemory مش محتاج heartbeat حقيقي، بس نضمن إنه موجود
        return Task.CompletedTask;
    }
    public Task<IReadOnlyCollection<UserId>> GetOnlineUsersAsync()
        => Task.FromResult<IReadOnlyCollection<UserId>>(
            _connections.Keys.Select(id => new UserId(id)).ToList());
}