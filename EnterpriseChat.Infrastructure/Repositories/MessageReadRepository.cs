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
        var currentUserIdValue = forUserId.Value;

        var rawQuery = _context.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId && !m.IsBlocked)
            .Include(m => m.ReplyToMessage)
            .OrderByDescending(m => m.CreatedAt);

        if (skip > 0) rawQuery = (IOrderedQueryable<Message>)rawQuery.Skip(skip);
        if (take > 0) rawQuery = (IOrderedQueryable<Message>)rawQuery.Take(take);

        // ✅ FIX: جيب الرسائل أولاً كـ list عشان نعمل client-side join
        var rawMessages = await rawQuery
            .OrderBy(m => m.CreatedAt)
            .AsSplitQuery()
            .Select(m => new
            {
                Message = m,
                Receipts = m.Receipts.Select(r => new { r.UserId, r.Status }),
                Reactions = m.Reactions.Select(r => new { r.UserId, r.Type }),
                ReplyToSenderId = m.ReplyToMessage != null ? m.ReplyToMessage.SenderId : null,
                ReplyToContent = m.ReplyToMessage != null ? m.ReplyToMessage.Content : null,
                ReplyToCreatedAt = m.ReplyToMessage != null ? m.ReplyToMessage.CreatedAt : (DateTime?)null,
                ReplyToIsDeleted = m.ReplyToMessage != null && m.ReplyToMessage.IsDeleted
            })
            .ToListAsync(ct);

        // ✅ FIX: جيب أسماء الـ senders للرسائل المرد عليها دفعة واحدة (batch)
        var replySenderIds = rawMessages
            .Where(x => x.ReplyToSenderId != null)
            .Select(x => x.ReplyToSenderId!.Value)
            .Distinct()
            .ToList();

        var senderNames = replySenderIds.Any()
            ? await _context.Users
                .Where(u => replySenderIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct)
            : new Dictionary<Guid, string>();

        var result = rawMessages.Select(x =>
        {
            var m = x.Message;
            var receipts = x.Receipts.ToList();
            var reactions = x.Reactions.ToList();

            var isSender = m.SenderId.Value == currentUserIdValue;

            MessageStatus personalStatus;
            if (isSender)
            {
                if (!receipts.Any())
                    personalStatus = MessageStatus.Sent;
                else if (receipts.All(r => r.Status >= MessageStatus.Read))
                    personalStatus = MessageStatus.Read;
                else if (receipts.All(r => r.Status >= MessageStatus.Delivered))
                    personalStatus = MessageStatus.Delivered;
                else
                    personalStatus = MessageStatus.Sent;
            }
            else
            {
                var myReceipt = receipts.FirstOrDefault(r => r.UserId.Value == currentUserIdValue);
                personalStatus = myReceipt?.Status ?? MessageStatus.Sent;
            }

            var deliveredCount = receipts.Count(r => r.Status >= MessageStatus.Delivered);
            var readCount = receipts.Count(r => r.Status >= MessageStatus.Read);

            MessageReactionsDto? reactionsDto = null;
            if (reactions.Any())
            {
                reactionsDto = new MessageReactionsDto
                {
                    MessageId = m.Id.Value,
                    Counts = reactions
                        .GroupBy(r => r.Type)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    CurrentUserReactionType = reactions
                        .FirstOrDefault(r => r.UserId.Value == currentUserIdValue)?.Type,
                    CurrentUserReaction = reactions.Any(r => r.UserId.Value == currentUserIdValue)
                        ? currentUserIdValue
                        : (Guid?)null
                };
            }

            // ✅ FIX: ReplyInfo مع SenderName من الـ dictionary
            ReplyInfoDto? replyInfo = null;
            if (m.ReplyToMessageId != null && x.ReplyToSenderId != null)
            {
                var senderName = senderNames.TryGetValue(x.ReplyToSenderId.Value, out var n) ? n : "";
                replyInfo = new ReplyInfoDto
                {
                    MessageId = m.ReplyToMessageId.Value,
                    SenderId = x.ReplyToSenderId.Value,
                    SenderName = senderName, // ✅ الاسم الحقيقي من DB
                    ContentPreview = x.ReplyToIsDeleted
                        ? "This message was deleted"
                        : (x.ReplyToContent?.Length > 100
                            ? x.ReplyToContent[..100]
                            : x.ReplyToContent ?? ""),
                    CreatedAt = x.ReplyToCreatedAt ?? DateTime.UtcNow,
                    IsDeleted = x.ReplyToIsDeleted
                };
            }

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
                IsEdited = m.IsEdited,
                IsDeleted = m.IsDeleted,
                ReplyToMessageId = m.ReplyToMessageId?.Value,
                ReplyInfo = replyInfo,
                Receipts = receipts.Select(r => new MessageReceiptDto
                {
                    UserId = r.UserId.Value,
                    Status = r.Status
                }).ToList(),
                Reactions = reactionsDto
            };
        }).ToList();

        foreach (var msg in result.Take(3))
            Console.WriteLine($"[REPO] Msg {msg.Id} personal={msg.PersonalStatus} replyInfo={msg.ReplyInfo != null} replySender={msg.ReplyInfo?.SenderName}");

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
                        !m.IsBlocked &&
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