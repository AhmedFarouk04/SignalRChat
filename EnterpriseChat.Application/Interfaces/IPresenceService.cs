using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IPresenceService
{
    Task UserConnectedAsync(UserId userId, string connectionId);
    Task UserDisconnectedAsync(UserId userId, string connectionId);
    Task<bool> IsOnlineAsync(UserId userId);
    Task<DateTime?> GetLastSeenAsync(UserId userId); // ➕ Last Seen
    Task<IReadOnlyCollection<UserId>> GetOnlineUsersAsync();
    Task<int> GetUserConnectionsCountAsync(UserId userId);
    Task UpdateHeartbeatAsync(UserId userId);
}