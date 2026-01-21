using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class RoomAuthorizationService : IRoomAuthorizationService
{
    private readonly ChatDbContext _context;

    public RoomAuthorizationService(ChatDbContext context)
    {
        _context = context;
    }

    public async Task EnsureUserIsMemberAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default)
    {
        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        if (room is null)
            throw new InvalidOperationException("Room not found.");

        var isMember = room.Members.Any(m => m.UserId.Value == userId.Value);
        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this room.");
    }

    public async Task EnsureUserIsOwnerAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default)
    {
        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        if (room is null)
            throw new InvalidOperationException("Room not found.");

        var isOwner =
            (room.OwnerId != null && room.OwnerId.Value == userId.Value) ||
            room.Members.Any(m => m.UserId.Value == userId.Value && m.IsOwner);

        if (!isOwner)
            throw new UnauthorizedAccessException("Only room owner can perform this action.");
    }

    public async Task EnsureUserIsAdminAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default)
    {
        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        if (room is null)
            throw new InvalidOperationException("Room not found.");

        if (room.OwnerId != null && room.OwnerId.Value == userId.Value)
            return;

        var isAdmin = room.Members.Any(m =>
            m.UserId.Value == userId.Value &&
            (m.IsAdmin || m.IsOwner));

        if (!isAdmin)
            throw new UnauthorizedAccessException("Only group admin can perform this action.");
    }
}
