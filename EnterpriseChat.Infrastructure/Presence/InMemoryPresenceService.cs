using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace EnterpriseChat.Infrastructure.Presence;

public sealed class InMemoryPresenceService : IPresenceService
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

    public Task UserConnectedAsync(UserId userId, string connectionId)
    {
        var connections = _connections.GetOrAdd(userId.Value, _ => new HashSet<string>());
        lock (connections)
        {
            connections.Add(connectionId);
        }
        return Task.CompletedTask;
    }

    public Task UserDisconnectedAsync(UserId userId, string connectionId)
    {
        if (_connections.TryGetValue(userId.Value, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                    _connections.TryRemove(userId.Value, out _);
            }
        }
        return Task.CompletedTask;
    }

    public Task<bool> IsOnlineAsync(UserId userId)
        => Task.FromResult(_connections.ContainsKey(userId.Value));

    public Task<IReadOnlyCollection<UserId>> GetOnlineUsersAsync()
        => Task.FromResult<IReadOnlyCollection<UserId>>(
            _connections.Keys.Select(id => new UserId(id)).ToList());
}
