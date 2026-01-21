using EnterpriseChat.Client.Models;

namespace EnterpriseChat.Client.Services.Rooms;

public interface IRoomService
{
    Task<IReadOnlyList<RoomModel>> GetRoomsAsync();
    Task<RoomModel?> GetRoomAsync(Guid roomId);
}
