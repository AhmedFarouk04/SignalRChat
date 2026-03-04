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

    public async Task UpdateMemberLastReadAsync(
        RoomId roomId,
        UserId userId,
        MessageId lastReadMessageId,
        CancellationToken ct = default)
    {
        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);

        if (member != null)
        {
            member.UpdateLastReadMessageId(lastReadMessageId);
            _context.Entry(member).Property<Guid?>("ChatUserId").CurrentValue = userId.Value;
            await _context.SaveChangesAsync(ct);
        }
    }

    public Task AddAsync(ChatRoom room, CancellationToken cancellationToken = default)
        => _context.ChatRooms.AddAsync(room, cancellationToken).AsTask();

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


    public async Task<IReadOnlyList<ChatRoom>> GetForUserAsync(
     UserId userId,
     CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
           
            .Include(r => r.Members)  // ❌ إلغاء AsSplitQuery
            .Where(r => r.Members.Any(m => m.UserId == userId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);  // ✅ استخدم cancellationToken
    }
    public async Task<ChatRoom?> GetByIdReadOnlyAsync(RoomId roomId, CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .AsNoTracking()  // ✅ للقراءة فقط
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);
    }

    public async Task<ChatRoom?> GetByIdForUpdateAsync(RoomId roomId, CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Members)  // ✅ بدون AsNoTracking - عشان tracking
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);
    }
    public Task DeleteAsync(ChatRoom room, CancellationToken ct = default)
    {
        _context.ChatRooms.Remove(room);
        return Task.CompletedTask;
    }

    public async Task<ChatRoom?> GetByIdWithPinsAsync(RoomId roomId, CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .Include(r => r.PinnedMessages)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);
    }

    // في Infrastructure/Repositories/ChatRoomRepository.cs
    public async Task<ChatRoom?> GetByIdAsync(RoomId roomId, CancellationToken ct = default)
    {
        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        // لو عايز تستخدم AsNoTracking للقراءة فقط، استخدم method منفصلة

        return room;
    }
    // أضف method جديدة للتحديث (أو عدل اللي موجودة)


    public async Task<ChatRoom?> GetByIdWithMembersAsync(
     RoomId roomId,
     CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);  // شيل AsNoTracking()
    }

    public async Task<bool> ExistsAsync(RoomId roomId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatRooms
            .AnyAsync(r => r.Id == roomId, cancellationToken);
    }
}