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

    public async Task RemoveAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default)
    {
        var entity = await _context.MutedRooms
            .FirstOrDefaultAsync(
                x => x.RoomId == roomId && x.UserId == userId,
                ct);

        if (entity != null)
            _context.MutedRooms.Remove(entity);
    }

    

    public async Task<IReadOnlyList<MutedRoom>> GetMutedRoomsAsync(UserId userId, CancellationToken ct = default)
    {
        return await _context.MutedRooms
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.MutedAt)
            .ToListAsync(ct);
    }

}
