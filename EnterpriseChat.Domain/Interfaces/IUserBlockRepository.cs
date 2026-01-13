using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Interfaces;

public interface IUserBlockRepository
{
    Task<bool> IsBlockedAsync(UserId a, UserId b, CancellationToken ct = default);

    Task AddAsync(BlockedUser block, CancellationToken ct = default);
}


