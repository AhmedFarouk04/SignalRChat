using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using StackExchange.Redis;

namespace EnterpriseChat.Infrastructure.Presence;

public sealed class RedisPresenceService : IPresenceService
{
    private readonly IDatabase _db;

    private const string Prefix = "presence:";

    public RedisPresenceService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    private static string Key(Guid userId)
        => $"{Prefix}{userId}";

    public async Task UserConnectedAsync(UserId userId, string connectionId)
    {
        await _db.SetAddAsync(Key(userId.Value), connectionId);
    }

    public async Task UserDisconnectedAsync(UserId userId, string connectionId)
    {
        await _db.SetRemoveAsync(Key(userId.Value), connectionId);
    }

    public async Task<bool> IsOnlineAsync(UserId userId)
    {
        return await _db.SetLengthAsync(Key(userId.Value)) > 0;
    }

    public async Task<IReadOnlyCollection<UserId>> GetOnlineUsersAsync()
    {
        var server = _db.Multiplexer
            .GetServer(_db.Multiplexer.GetEndPoints().First());

        var keys = server.Keys(pattern: $"{Prefix}*");

        var users = new List<UserId>();

        foreach (var key in keys)
        {
            var id = key.ToString().Replace(Prefix, "");
            if (Guid.TryParse(id, out var guid))
                users.Add(new UserId(guid));
        }

        return users;
    }
}
