using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IRoomPresenceService
{
    Task JoinRoomAsync(RoomId roomId, UserId userId);
    Task LeaveRoomAsync(RoomId roomId, UserId userId);

    Task<int> GetOnlineCountAsync(RoomId roomId);
    Task<IReadOnlyCollection<UserId>> GetOnlineUsersAsync(RoomId roomId);

    Task<IReadOnlyCollection<RoomId>> RemoveUserFromAllRoomsAsync(UserId userId);
}
