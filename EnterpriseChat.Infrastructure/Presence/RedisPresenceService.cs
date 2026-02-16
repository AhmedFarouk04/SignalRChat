using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using StackExchange.Redis;

public sealed class RedisPresenceService : IPresenceService
{
    private readonly IDatabase _db;
    private const string Prefix = "presence:";
    private const string LastSeenPrefix = "lastseen:"; // ➕ إضافة Last Seen prefix
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private static string Key(Guid userId) => $"{Prefix}{userId}";
    private static string LastSeenKey(Guid userId) => $"{LastSeenPrefix}{userId}"; // ➕

    public RedisPresenceService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<int> GetUserConnectionsCountAsync(UserId userId)
    {
        var key = Key(userId.Value);
        return (int)await _db.SetLengthAsync(key);
    }
    public async Task CleanupStaleConnectionsAsync()
    {
        try
        {
            var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{Prefix}*").ToArray();

            foreach (var key in keys)
            {
                var ttl = await _db.KeyTimeToLiveAsync(key);
                if (ttl == null || ttl <= TimeSpan.Zero || ttl < TimeSpan.FromSeconds(10))
                {
                    var id = key.ToString().Replace(Prefix, "");
                    if (Guid.TryParse(id, out var userId))
                    {
                        Console.WriteLine($"[RedisPresence] Cleaning up stale connection for user {userId}");
                        await _db.KeyDeleteAsync(key);

                        // تسجيل Last Seen
                        await _db.StringSetAsync(LastSeenKey(userId), DateTime.UtcNow.Ticks, TimeSpan.FromDays(30));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RedisPresence] Cleanup error: {ex.Message}");
        }
    }
    public async Task UpdateHeartbeatAsync(UserId userId)
    {
        try
        {
            var heartbeatKey = $"heartbeat:{userId.Value}";
            await _db.StringSetAsync(heartbeatKey, DateTime.UtcNow.Ticks, TimeSpan.FromSeconds(20));

            // كمان نجدد الـ TTL للـ presence key
            var presenceKey = Key(userId.Value);
            await _db.KeyExpireAsync(presenceKey, TimeSpan.FromMinutes(1));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RedisPresence] Heartbeat error: {ex.Message}");
        }
    }

    public async Task<DateTime?> GetLastHeartbeatAsync(UserId userId)
    {
        var val = await _db.StringGetAsync($"heartbeat:{userId.Value}");
        return val.HasValue ? new DateTime((long)val) : (DateTime?)null;
    }
    public async Task UserConnectedAsync(UserId userId, string connectionId)
    {
        var key = Key(userId.Value);
        await _db.SetAddAsync(key, connectionId);

        // ✅ زود الـ TTL لدقيقتين بدل 30 ثانية
        await _db.KeyExpireAsync(key, TimeSpan.FromMinutes(2));

        // ✅ حذف Last Seen عند الاتصال
        await _db.KeyDeleteAsync(LastSeenKey(userId.Value));

        // ✅ Heartbeat كل 30 ثانية بدل 10
        var heartbeatKey = $"heartbeat:{userId.Value}";
        await _db.StringSetAsync(heartbeatKey, DateTime.UtcNow.Ticks, TimeSpan.FromSeconds(30));
    }
    public async Task UserDisconnectedAsync(UserId userId, string connectionId)
    {
        var key = Key(userId.Value);
        await _db.SetRemoveAsync(key, connectionId);

        if (await _db.SetLengthAsync(key) == 0)
        {
            await _db.KeyDeleteAsync(key);
            // ➕ تسجيل Last Seen عند قطع آخر اتصال فوراً
            await _db.StringSetAsync(LastSeenKey(userId.Value), DateTime.UtcNow.Ticks, TimeSpan.FromDays(30));

            // ✅ حذف الـ heartbeat فوراً
            await _db.KeyDeleteAsync($"heartbeat:{userId.Value}");

            Console.WriteLine($"[RedisPresence] User {userId} completely disconnected, last seen recorded");
        }
        else
        {
            await _db.KeyExpireAsync(key, TimeSpan.FromSeconds(30));
        }
    }

    public async Task<bool> IsOnlineAsync(UserId userId)
        => await _db.SetLengthAsync(Key(userId.Value)) > 0;

    public async Task<DateTime?> GetLastSeenAsync(UserId userId)
    {
        try
        {
            var val = await _db.StringGetAsync($"lastseen:{userId.Value}");
            return val.HasValue ? new DateTime((long)val) : (DateTime?)null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyCollection<UserId>> GetOnlineUsersAsync()
    {
        try
        {
            var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{Prefix}*").ToArray();

            var users = new List<UserId>();
            var now = DateTime.UtcNow;

            foreach (var key in keys)
            {
                try
                {
                    // تحقق من وجود المفتاح وفعاليته
                    if (!await _db.KeyExistsAsync(key))
                        continue;

                    // تحقق من TTL
                    var ttl = await _db.KeyTimeToLiveAsync(key);

                    // ✅ TTL أقصر من 10 ثواني معناه إن المستخدم على وشك الانتهاء
                    if (ttl == null || ttl <= TimeSpan.Zero || ttl < TimeSpan.FromSeconds(5))
                    {
                        // لسه بنعتبره أونلاين لو الـ TTL قريب
                        var id = key.ToString().Replace(Prefix, "");
                        if (Guid.TryParse(id, out var guid))
                        {
                            var connectionCount = await _db.SetLengthAsync(key);
                            if (connectionCount > 0)
                            {
                                users.Add(new UserId(guid));
                                Console.WriteLine($"[RedisPresence] User {guid} is online with low TTL: {ttl?.TotalSeconds}s");
                                continue;
                            }
                        }

                        await _db.KeyDeleteAsync(key);
                        continue;
                    }

                    var idStr = key.ToString().Replace(Prefix, "");
                    if (Guid.TryParse(idStr, out var userId))
                    {
                        // تحقق إضافي: هل لسه في connections؟
                        var connectionCount = await _db.SetLengthAsync(key);
                        if (connectionCount > 0)
                        {
                            users.Add(new UserId(userId));
                        }
                        else
                        {
                            // لو مفيش connections، احذف المفتاح
                            await _db.KeyDeleteAsync(key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RedisPresence] Error processing key {key}: {ex.Message}");
                }
            }

            Console.WriteLine($"[RedisPresence] GetOnlineUsersAsync returning {users.Count} users");
            return users;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RedisPresence] GetOnlineUsersAsync error: {ex.Message}");
            return new List<UserId>();
        }
    }
}