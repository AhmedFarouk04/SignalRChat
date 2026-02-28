using EnterpriseChat.Client.Models;

namespace EnterpriseChat.Client.Services.Rooms;

public interface IRoomService
{
    Task<IReadOnlyList<RoomListItemModel>> GetRoomsAsync();
    Task<RoomModel?> GetRoomAsync(Guid roomId);
    Task<List<UserModel>> GetGroupMembersAsync(Guid groupId);

}
