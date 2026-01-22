using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Interfaces;

public interface IMutedRoomRepository
{
    Task<bool> IsMutedAsync(RoomId roomId, UserId userId, CancellationToken ct = default);

    Task AddAsync(MutedRoom mute, CancellationToken ct = default);

    Task RemoveAsync(RoomId roomId, UserId userId, CancellationToken ct = default);

    Task<IReadOnlyList<MutedRoom>> GetMutedRoomsAsync(UserId userId, CancellationToken ct = default);
}
