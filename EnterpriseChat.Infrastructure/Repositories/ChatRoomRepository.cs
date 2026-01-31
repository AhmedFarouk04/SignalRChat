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

    public Task AddAsync(ChatRoom room, CancellationToken cancellationToken = default)
        => _context.ChatRooms.AddAsync(room, cancellationToken).AsTask();

    public async Task<ChatRoom?> GetByIdAsync(RoomId roomId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId.Value, cancellationToken);
    }

    public async Task<ChatRoom?> GetByIdWithMembersAsync(RoomId roomId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId.Value, cancellationToken);
    }

    public async Task<bool> ExistsAsync(RoomId roomId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
            .AnyAsync(r => r.Id == roomId.Value, cancellationToken);
    }

    public async Task<ChatRoom?> FindPrivateRoomAsync(UserId a, UserId b, CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Members)
            .Where(r =>
                r.Type == RoomType.Private &&
                r.Members.Any(m => m.UserId == a) &&
                r.Members.Any(m => m.UserId == b))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ChatRoom>> GetForUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Members)
            .Where(r => r.Members.Any(m => m.UserId == userId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteAsync(ChatRoom room, CancellationToken ct = default)
    {
        _context.ChatRooms.Remove(room);
        return Task.CompletedTask;
    }
}
