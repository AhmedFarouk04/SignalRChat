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
        UserId forUserId,
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

        var currentUserIdValue = forUserId.Value;

        var messages = await query
            .Include(m => m.Receipts)
            .Select(m => new
            {
                Message = m,
                Receipts = m.Receipts
            })
            .ToListAsync(ct);

        var result = messages.Select(x =>
        {
            var m = x.Message;
            var receipts = x.Receipts.ToList();
            var isSender = m.SenderId.Value == currentUserIdValue;

            MessageStatus personalStatus;

            if (isSender)
            {
                // ✅ حالة الرسالة بالنسبة للـ sender: تعتمد على حالة المستلمين
                if (!receipts.Any())
                {
                    personalStatus = MessageStatus.Sent;
                }
                else if (receipts.All(r => r.Status >= MessageStatus.Read))
                {
                    personalStatus = MessageStatus.Read;
                }
                else if (receipts.All(r => r.Status >= MessageStatus.Delivered))
                {
                    personalStatus = MessageStatus.Delivered;
                }
                else
                {
                    personalStatus = MessageStatus.Sent;
                }
            }
            else
            {
                // ✅ حالة الرسالة بالنسبة للمستلم: receipt بتاعي أنا
                var myReceipt = receipts.FirstOrDefault(r => r.UserId.Value == currentUserIdValue);
                personalStatus = myReceipt?.Status ?? MessageStatus.Sent;
            }

            var deliveredCount = receipts.Count(r => r.Status >= MessageStatus.Delivered);
            var readCount = receipts.Count(r => r.Status >= MessageStatus.Read);

            return new MessageReadDto
            {
                Id = m.Id.Value,
                RoomId = m.RoomId.Value,
                SenderId = m.SenderId.Value,
                Content = m.Content,
                CreatedAt = m.CreatedAt,

                PersonalStatus = personalStatus,

                DeliveredCount = deliveredCount,
                ReadCount = readCount,

                Receipts = receipts.Select(r => new MessageReceiptDto
                {
                    UserId = r.UserId.Value,
                    Status = r.Status
                }).ToList()
            };
        }).ToList();

        // Debug بسيط
        foreach (var msg in result.Take(3))
        {
            Console.WriteLine($"[REPO] Msg {msg.Id} sender={msg.SenderId} personal={msg.PersonalStatus} deliveredCount={msg.DeliveredCount} readCount={msg.ReadCount}");
        }

        return result;
    }

    public async Task<IReadOnlyList<MessageReadDto>> SearchMessagesAsync(
        RoomId roomId,
        string searchTerm,
        int take,
        CancellationToken ct)
    {
        var term = searchTerm.Trim().ToLower();

        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId &&
                        !m.IsDeleted &&
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

                // في البحث (مش شخصي) — نعرض “أعلى حالة” كمؤشر عام
                PersonalStatus = m.Receipts.Any(r => r.Status >= MessageStatus.Read)
                    ? MessageStatus.Read
                    : m.Receipts.Any(r => r.Status >= MessageStatus.Delivered)
                        ? MessageStatus.Delivered
                        : MessageStatus.Sent,

                DeliveredCount = m.Receipts.Count(r => r.Status >= MessageStatus.Delivered),
                ReadCount = m.Receipts.Count(r => r.Status >= MessageStatus.Read),

                Receipts = m.Receipts.Select(r => new MessageReceiptDto
                {
                    UserId = r.UserId.Value,
                    Status = r.Status
                }).ToList()
            })
            .ToListAsync(ct);
    }

    // الدالة دي تقدر تحذفها لو مش بتستخدمها
    private static MessageStatus CalculateMessageStatusForSender(Message m)
    {
        var recipientReceipts = m.Receipts
            .Where(r => r.UserId != m.SenderId)
            .ToList();

        if (!recipientReceipts.Any())
            return MessageStatus.Sent;

        if (recipientReceipts.All(r => r.Status == MessageStatus.Read))
            return MessageStatus.Read;

        if (recipientReceipts.All(r => r.Status >= MessageStatus.Delivered))
            return MessageStatus.Delivered;

        return MessageStatus.Sent;
    }
}
