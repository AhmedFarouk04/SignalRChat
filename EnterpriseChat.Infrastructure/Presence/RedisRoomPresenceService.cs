using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using StackExchange.Redis;

namespace EnterpriseChat.Infrastructure.Presence;

public sealed class RedisRoomPresenceService : IRoomPresenceService
{
    private readonly IDatabase _db;

    private const string RoomPrefix = "presence:room:";
    private const string UserPrefix = "presence:user:";

    public RedisRoomPresenceService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }
    public async Task<bool> IsUserInRoomAsync(RoomId roomId, UserId userId)
    {
        return await _db.SetContainsAsync(RoomUsersKey(roomId.Value), userId.Value.ToString());
    }
    private static string RoomUsersKey(Guid roomId) => $"{RoomPrefix}{roomId}:users";
    private static string UserRoomsKey(Guid userId) => $"{UserPrefix}{userId}:rooms";

    public async Task JoinRoomAsync(RoomId roomId, UserId userId)
    {
        await _db.SetAddAsync(RoomUsersKey(roomId.Value), userId.Value.ToString());
        await _db.SetAddAsync(UserRoomsKey(userId.Value), roomId.Value.ToString());

        // ✅ إضافة TTL للغرفة (اختياري)
        await _db.KeyExpireAsync(RoomUsersKey(roomId.Value), TimeSpan.FromHours(1));
    }

    public async Task LeaveRoomAsync(RoomId roomId, UserId userId)
    {
        await _db.SetRemoveAsync(RoomUsersKey(roomId.Value), userId.Value.ToString());
        await _db.SetRemoveAsync(UserRoomsKey(userId.Value), roomId.Value.ToString());
    }

    public async Task<int> GetOnlineCountAsync(RoomId roomId)
        => (int)await _db.SetLengthAsync(RoomUsersKey(roomId.Value));

    public async Task<IReadOnlyCollection<UserId>> GetOnlineUsersAsync(RoomId roomId)
    {
        var members = await _db.SetMembersAsync(RoomUsersKey(roomId.Value));
        var result = new List<UserId>();

        foreach (var m in members)
        {
            if (Guid.TryParse(m.ToString(), out var id))
                result.Add(new UserId(id));
        }

        return result;
    }

    public async Task<IReadOnlyCollection<RoomId>> RemoveUserFromAllRoomsAsync(UserId userId)
    {
        var rooms = await _db.SetMembersAsync(UserRoomsKey(userId.Value));
        var removedRooms = new List<RoomId>();

        foreach (var r in rooms)
        {
            if (!Guid.TryParse(r.ToString(), out var roomGuid))
                continue;

            var roomId = new RoomId(roomGuid);

            await _db.SetRemoveAsync(RoomUsersKey(roomGuid), userId.Value.ToString());
            removedRooms.Add(roomId);
        }

        await _db.KeyDeleteAsync(UserRoomsKey(userId.Value));

        return removedRooms;
    }
}
