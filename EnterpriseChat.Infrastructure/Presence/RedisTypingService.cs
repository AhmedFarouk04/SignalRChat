using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using StackExchange.Redis;

namespace EnterpriseChat.Infrastructure.Presence;

public sealed class RedisTypingService : ITypingService
{
    private readonly IDatabase _db;
    private const string Prefix = "typing:";
    private const string RoomTypingPrefix = "typing:room:"; 
    public RedisTypingService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    private static string Key(Guid roomId, Guid userId)
        => $"{Prefix}room:{roomId}:user:{userId}";

        private static string RoomKey(Guid roomId)
        => $"{RoomTypingPrefix}{roomId}";

    public async Task<bool> StartTypingAsync(RoomId roomId, UserId userId, TimeSpan ttl)
    {
        var key = Key(roomId.Value, userId.Value);
        var roomKey = RoomKey(roomId.Value);

        var exists = await _db.KeyExistsAsync(key);

                await _db.SetAddAsync(roomKey, userId.Value.ToString());

        if (exists)
        {
            await _db.KeyExpireAsync(key, ttl);
            await _db.KeyExpireAsync(roomKey, ttl);             return false;
        }

        await _db.StringSetAsync(key, "1", ttl);
        await _db.KeyExpireAsync(roomKey, ttl); 
        return true;
    }

    public async Task StopTypingAsync(RoomId roomId, UserId userId)
    {
        var key = Key(roomId.Value, userId.Value);
        var roomKey = RoomKey(roomId.Value);

        await _db.KeyDeleteAsync(key);
        await _db.SetRemoveAsync(roomKey, userId.Value.ToString());     }

        public async Task<IReadOnlyList<UserId>> GetTypingUsersAsync(RoomId roomId)
    {
        var roomKey = RoomKey(roomId.Value);
        var members = await _db.SetMembersAsync(roomKey);

        var result = new List<UserId>();
        foreach (var m in members)
        {
            if (Guid.TryParse(m.ToString(), out var id))
            {
                                var userKey = Key(roomId.Value, id);
                if (await _db.KeyExistsAsync(userKey))
                {
                    result.Add(new UserId(id));
                }
                else
                {
                                        await _db.SetRemoveAsync(roomKey, m.ToString());
                }
            }
        }

        return result;
    }

        public async Task<bool> IsTypingAsync(RoomId roomId, UserId userId)
    {
        return await _db.KeyExistsAsync(Key(roomId.Value, userId.Value));
    }
}