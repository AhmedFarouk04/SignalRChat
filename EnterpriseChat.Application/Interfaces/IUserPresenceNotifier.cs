namespace EnterpriseChat.Application.Interfaces;

public interface IUserPresenceNotifier
{
    Task HideUsersFromEachOtherAsync(Guid a, Guid b, CancellationToken ct = default);
    Task BlockChangedAsync(Guid targetUserId, Guid otherUserId, bool blocked, CancellationToken ct = default);
}
