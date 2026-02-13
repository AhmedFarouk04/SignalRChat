using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IMessageReadRepository
{
    Task<IReadOnlyList<MessageReadDto>> GetMessagesAsync(
            RoomId roomId,
            UserId forUserId,          
            int skip,
            int take,
            CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageReadDto>> SearchMessagesAsync(RoomId roomId, string searchTerm, int take, CancellationToken ct);
}
