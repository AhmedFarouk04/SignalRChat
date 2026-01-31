using EnterpriseChat.Application.DTOs;

namespace EnterpriseChat.Application.Interfaces;

public interface IRoomDetailsReader
{
    Task<RoomDetailsDto?> GetRoomDetailsAsync(Guid roomId, Guid viewerId, CancellationToken ct);
}
