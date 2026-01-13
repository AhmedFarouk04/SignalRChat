using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IPresenceService
{
    Task UserConnectedAsync(UserId userId, string connectionId);
    Task UserDisconnectedAsync(UserId userId, string connectionId);

    Task<bool> IsOnlineAsync(UserId userId);
    Task<IReadOnlyCollection<UserId>> GetOnlineUsersAsync();
}
