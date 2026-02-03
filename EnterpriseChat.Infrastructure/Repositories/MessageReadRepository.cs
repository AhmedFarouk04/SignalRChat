using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class MessageReadRepository : IMessageReadRepository
{
    private readonly ChatDbContext _context;

    public MessageReadRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MessageReadDto>> GetMessagesAsync(
     RoomId roomId,
     int skip = 0,
     int take = 100,
     CancellationToken ct = default)
    {
        IQueryable<Message> query = _context.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId);

        query = query.OrderByDescending(m => m.CreatedAt);

        if (skip > 0) query = query.Skip(skip);
        if (take > 0) query = query.Take(take);

        var messages = await query
            .Select(m => new MessageReadDto
            {
                Id = m.Id.Value,
                RoomId = m.RoomId.Value,
                SenderId = m.SenderId.Value,
                Content = m.Content,
                CreatedAt = m.CreatedAt,

                // ✅ أهم سطرين: حساب الـ Status على مستوى الرسالة
                Status =
                    m.Receipts.Any(r => r.Status == MessageStatus.Read) ? MessageStatus.Read :
                    m.Receipts.Any(r => r.Status == MessageStatus.Delivered) ? MessageStatus.Delivered :
                    MessageStatus.Sent,

                Receipts = m.Receipts.Select(r => new MessageReceiptDto
                {
                    UserId = r.UserId.Value,
                    Status = r.Status
                }).ToList()
            })
            .ToListAsync(ct);

        return messages;
    }

}