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
    // EnterpriseChat.Infrastructure/Repositories/MessageReadRepository.cs
    // EnterpriseChat.Infrastructure/Repositories/MessageReadRepository.cs

    // EnterpriseChat.Infrastructure/Repositories/MessageReadRepository.cs

    public async Task<IReadOnlyList<MessageReadDto>> SearchMessagesAsync(
        RoomId roomId,
        string searchTerm,
        int take,
        CancellationToken ct)
    {
        var term = searchTerm.Trim().ToLower();

        // ملاحظة: شيلنا m.IsSystem واستبدلناها بمنطق يتناسب مع الـ Entity بتاعتك
        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId &&
                        !m.IsDeleted &&
                        // إذا كنت لا تريد البحث في الرسائل الفارغة أو النظامية (حسب الحاجة)
                        !string.IsNullOrEmpty(m.Content) &&
                        m.Content.ToLower().Contains(term))
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .Select(m => new MessageReadDto
            {
                Id = m.Id.Value,
                RoomId = m.RoomId.Value,
                SenderId = m.SenderId.Value,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
                Status = m.Receipts.Any(r => r.Status == MessageStatus.Read) ? MessageStatus.Read :
                         m.Receipts.Any(r => r.Status == MessageStatus.Delivered) ? MessageStatus.Delivered :
                         MessageStatus.Sent
            })
            .ToListAsync(ct);
    }

}