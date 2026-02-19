// Infrastructure/Presence/RedisTypingService.cs
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using StackExchange.Redis;

namespace EnterpriseChat.Infrastructure.Presence;

public sealed class RedisTypingService : ITypingService
{
    private readonly IDatabase _db;
    private const string Prefix = "typing:";
    private const string RoomTypingPrefix = "typing:room:"; // ➕ جديد

    public RedisTypingService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    private static string Key(Guid roomId, Guid userId)
        => $"{Prefix}room:{roomId}:user:{userId}";

    // ➕ مفتاح جديد لتخزين كل المستخدمين اللي بيكتبوا في Room معين
    private static string RoomKey(Guid roomId)
        => $"{RoomTypingPrefix}{roomId}";

    public async Task<bool> StartTypingAsync(RoomId roomId, UserId userId, TimeSpan ttl)
    {
        var key = Key(roomId.Value, userId.Value);
        var roomKey = RoomKey(roomId.Value);

        var exists = await _db.KeyExistsAsync(key);

        // ✅ تخزين المستخدم في Set خاص بالـ Room
        await _db.SetAddAsync(roomKey, userId.Value.ToString());

        if (exists)
        {
            await _db.KeyExpireAsync(key, ttl);
            await _db.KeyExpireAsync(roomKey, ttl); // ➕ تمديد للـ Room set كمان
            return false;
        }

        await _db.StringSetAsync(key, "1", ttl);
        await _db.KeyExpireAsync(roomKey, ttl); // ➕ TTL للـ Room set

        return true;
    }

    public async Task StopTypingAsync(RoomId roomId, UserId userId)
    {
        var key = Key(roomId.Value, userId.Value);
        var roomKey = RoomKey(roomId.Value);

        await _db.KeyDeleteAsync(key);
        await _db.SetRemoveAsync(roomKey, userId.Value.ToString()); // ➕ إزالة من Set
    }

    // ➕ دالة جديدة لجلب كل المستخدمين اللي بيكتبوا في Room
    public async Task<IReadOnlyList<UserId>> GetTypingUsersAsync(RoomId roomId)
    {
        var roomKey = RoomKey(roomId.Value);
        var members = await _db.SetMembersAsync(roomKey);

        var result = new List<UserId>();
        foreach (var m in members)
        {
            if (Guid.TryParse(m.ToString(), out var id))
            {
                // ✅ التحقق إن المستخدم لسه فعلاً بيكتب (مش expired)
                var userKey = Key(roomId.Value, id);
                if (await _db.KeyExistsAsync(userKey))
                {
                    result.Add(new UserId(id));
                }
                else
                {
                    // تنظيف الـ stale entries
                    await _db.SetRemoveAsync(roomKey, m.ToString());
                }
            }
        }

        return result;
    }

    // ➕ دالة للتحقق من أن المستخدم بيكتب دلوقتي
    public async Task<bool> IsTypingAsync(RoomId roomId, UserId userId)
    {
        return await _db.KeyExistsAsync(Key(roomId.Value, userId.Value));
    }
}