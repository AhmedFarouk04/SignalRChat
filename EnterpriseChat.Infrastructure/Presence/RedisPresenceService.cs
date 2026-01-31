using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using StackExchange.Redis;

public sealed class RedisPresenceService : IPresenceService
{
    private readonly IDatabase _db;
    private const string Prefix = "presence:";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private static string Key(Guid userId) => $"{Prefix}{userId}";

    public RedisPresenceService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task UserConnectedAsync(UserId userId, string connectionId)
    {
        var key = Key(userId.Value);
        await _db.SetAddAsync(key, connectionId);

        // ✅ TTL (يتجدد مع كل connect)
        await _db.KeyExpireAsync(key, Ttl);
    }

    public async Task UserDisconnectedAsync(UserId userId, string connectionId)
    {
        var key = Key(userId.Value);
        await _db.SetRemoveAsync(key, connectionId);

        // ✅ لو فاضي امسحه
        if (await _db.SetLengthAsync(key) == 0)
            await _db.KeyDeleteAsync(key);
        else
            await _db.KeyExpireAsync(key, Ttl); // اختياري
    }

    public async Task<bool> IsOnlineAsync(UserId userId)
        => await _db.SetLengthAsync(Key(userId.Value)) > 0;

    public async Task<IReadOnlyCollection<UserId>> GetOnlineUsersAsync()
    {
        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
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
