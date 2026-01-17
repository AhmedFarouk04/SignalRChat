using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using StackExchange.Redis;

namespace EnterpriseChat.Infrastructure.Presence;

public sealed class RedisTypingService : ITypingService
{
    private readonly IDatabase _db;
    private const string Prefix = "typing:";

    public RedisTypingService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    private static string Key(Guid roomId, Guid userId)
        => $"{Prefix}room:{roomId}:user:{userId}";

    public async Task<bool> StartTypingAsync(RoomId roomId, UserId userId, TimeSpan ttl)
    {
        var key = Key(roomId.Value, userId.Value);

        if (await _db.KeyExistsAsync(key))
        {
            await _db.KeyExpireAsync(key, ttl);
            return false;
        }

        await _db.StringSetAsync(key, "1", ttl);
        return true;
    }

    public async Task StopTypingAsync(RoomId roomId, UserId userId)
    {
        await _db.KeyDeleteAsync(Key(roomId.Value, userId.Value));
    }
}
