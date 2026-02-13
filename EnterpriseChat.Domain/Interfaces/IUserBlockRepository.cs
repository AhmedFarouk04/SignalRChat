using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Interfaces;

public interface IUserBlockRepository
{
    Task<bool> IsBlockedAsync(UserId a, UserId b, CancellationToken ct = default);

    Task AddAsync(BlockedUser block, CancellationToken ct = default);

    Task RemoveAsync(UserId blockerId, UserId blockedId, CancellationToken ct = default);
    Task<IReadOnlyList<BlockedUser>> GetBlockersOfUserAsync(UserId userId, CancellationToken ct = default);
    Task<IReadOnlyList<BlockedUser>> GetBlockedByBlockerAsync(UserId blockerId, CancellationToken ct = default);
}
