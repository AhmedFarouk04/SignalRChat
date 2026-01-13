using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IMessageReadRepository
{
    Task<IReadOnlyList<MessageReadDto>> GetMessagesAsync(
        RoomId roomId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
