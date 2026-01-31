using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class MutedRoomRepository : IMutedRoomRepository
{
    private readonly ChatDbContext _context;

    public MutedRoomRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsMutedAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default)
    {
        return await _context.MutedRooms.AnyAsync(
            x => x.RoomId == roomId && x.UserId == userId,
            ct);
    }

    public async Task AddAsync(MutedRoom mute, CancellationToken ct = default)
    {
        await _context.MutedRooms.AddAsync(mute, ct);
    }

    public async Task RemoveAsync(RoomId roomId, UserId userId, CancellationToken ct = default)
    {
        var entities = await _context.MutedRooms
            .Where(x => x.RoomId == roomId && x.UserId == userId)
            .ToListAsync(ct);

        if (entities.Count > 0)
            _context.MutedRooms.RemoveRange(entities);
    }



    public async Task<IReadOnlyList<MutedRoom>> GetMutedRoomsAsync(UserId userId, CancellationToken ct = default)
    {
        return await _context.MutedRooms
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.MutedAt)
            .ToListAsync(ct);
    }

    public async Task<HashSet<Guid>> GetMutedRoomIdsAsync(UserId userId, CancellationToken ct)
    {
        var ids = await _context.MutedRooms
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.RoomId.Value) // حسب نوع RoomId عندك
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

}
