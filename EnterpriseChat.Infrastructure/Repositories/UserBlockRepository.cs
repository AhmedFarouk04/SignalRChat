using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class UserBlockRepository : IUserBlockRepository
{
    private readonly ChatDbContext _context;

    public UserBlockRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsBlockedAsync(UserId a, UserId b, CancellationToken ct = default)
    {
        return await _context.BlockedUsers.AnyAsync(
            x =>
                (x.BlockerId == a && x.BlockedId == b) ||
                (x.BlockerId == b && x.BlockedId == a),
            ct);
    }

    public async Task AddAsync(BlockedUser block, CancellationToken ct = default)
    {
        await _context.BlockedUsers.AddAsync(block, ct);
    }

    public async Task RemoveAsync(UserId blockerId, UserId blockedId, CancellationToken ct = default)
    {
        var entity = await _context.BlockedUsers.FirstOrDefaultAsync(
            x => x.BlockerId == blockerId && x.BlockedId == blockedId,
            ct);

        if (entity != null)
            _context.BlockedUsers.Remove(entity);
    }

    public async Task<IReadOnlyList<BlockedUser>> GetBlockedByBlockerAsync(UserId blockerId, CancellationToken ct = default)
    {
        return await _context.BlockedUsers
            .AsNoTracking()
            .Where(x => x.BlockerId == blockerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }
}
