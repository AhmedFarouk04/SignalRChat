using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class ChatRoomRepository : IChatRoomRepository
{
    private readonly ChatDbContext _context;

    public ChatRoomRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ChatRoom room, CancellationToken cancellationToken = default)
    {
        await _context.ChatRooms.AddAsync(room, cancellationToken);
    }

    public async Task<ChatRoom?> GetByIdAsync(RoomId roomId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
            .FirstOrDefaultAsync(c => c.Id == roomId);
    }

    public async Task<bool> ExistsAsync(RoomId roomId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
            .AnyAsync(x => x.Id.Value == roomId.Value, cancellationToken);
    }

    public async Task<ChatRoom?> FindPrivateRoomAsync(
    UserId a,
    UserId b,
    CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Members)
            .Where(r =>
                r.Type == RoomType.Private &&
                r.Members.Any(m => m.UserId == a) &&
                r.Members.Any(m => m.UserId == b))
            .FirstOrDefaultAsync(ct);
    }
    public async Task<IReadOnlyList<ChatRoom>> GetForUserAsync(
    UserId userId,
    CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Members)
            .Where(r => r.Members.Any(m => m.UserId == userId))
            .OrderByDescending(r => r.CreatedAt) // لو عندك CreatedAt
            .ToListAsync(cancellationToken);
    }


}
