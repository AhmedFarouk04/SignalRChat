using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface ITypingService
{
    Task<bool> StartTypingAsync(RoomId roomId, UserId userId, TimeSpan ttl);

    Task StopTypingAsync(RoomId roomId, UserId userId);
    Task<IReadOnlyList<UserId>> GetTypingUsersAsync(RoomId roomId);
    Task<bool> IsTypingAsync(RoomId roomId, UserId userId);
}
