using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class RoomDetailsReader : IRoomDetailsReader
{
    private readonly ChatDbContext _context;

    public RoomDetailsReader(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<RoomDetailsDto?> GetRoomDetailsAsync(Guid roomId, Guid viewerId, CancellationToken ct)
    {
        var room = await _context.ChatRooms
            .AsNoTracking()
            .Include(r => r.Members)
            .AsSplitQuery() // ✅ أضف هنا
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        if (room is null) return null;

        Guid? otherUserId = null;
        string? otherDisplayName = null;

        if (room.Type == RoomType.Private)
        {
            otherUserId = room.Members
                .Select(m => (Guid?)m.UserId.Value)
                .FirstOrDefault(x => x != viewerId);

            if (otherUserId != null)
            {
                otherDisplayName = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == otherUserId.Value)
                    .Select(u => u.DisplayName)
                    .FirstOrDefaultAsync(ct);
            }
        }

        return new RoomDetailsDto(
            room.Id,
            room.Name,
            room.Type.ToString(),
            otherUserId,
            otherDisplayName
        );
    }
}