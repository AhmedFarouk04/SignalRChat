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

        // ✅ استخدم ct العادي هنا، لكن مع إضافة timeout
        var deletedMessageIds = await _context.Set<MessageDeletion>()
            .Where(d => d.UserId == forUserId)
            .Join(
                _context.Messages.Where(m => m.RoomId == roomId),
                d => d.MessageId,
                m => m.Id,
                (d, m) => d.MessageId)
            .ToListAsync(ct);   // ✅ استخدم ct العادي

        // ✅ الحل: حول من SplitQuery لـ Include مع ThenInclude
        var rawMessages = await _context.Messages
            // ❌ إلغاء AsSplitQuery
            // .AsSplitQuery()
            .Include(m => m.Receipts)      // ✅ استخدم Include بدل SplitQuery
            .Include(m => m.Reactions)
            .Include(m => m.ReplyToMessage)
            .Where(m => m.RoomId == roomId
                && !m.IsBlocked
                && !deletedMessageIds.Contains(m.Id))
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip > 0 ? skip : 0)
            .Take(take > 0 ? take : 100)
            .Select(m => new
            {
                Message = m,
                Receipts = m.Receipts.Select(r => new { r.UserId, r.Status }).ToList(),
                Reactions = m.Reactions.Select(r => new { r.UserId, r.Type }).ToList(),
                ReplyToSenderId = m.ReplyToMessage != null ? m.ReplyToMessage.SenderId : null,
                ReplyToContent = m.ReplyToMessage != null ? m.ReplyToMessage.Content : null,
                ReplyToCreatedAt = m.ReplyToMessage != null ? m.ReplyToMessage.CreatedAt : (DateTime?)null,
                ReplyToIsDeleted = m.ReplyToMessage != null && m.ReplyToMessage.IsDeleted
            })
            .OrderBy(m => m.Message.CreatedAt)
            .ToListAsync(ct);   // ✅ استخدم ct العادي   // ← None — ده أهم سطر

        // ✅ Batch fetch sender names — query واحدة بدل N+1
        var replySenderIds = rawMessages
            .Where(x => x.ReplyToSenderId != null)
            .Select(x => x.ReplyToSenderId!.Value)
            .Distinct()
            .ToList();

        var senderNames = replySenderIds.Any()
            ? await _context.Users
                .Where(u => replySenderIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, CancellationToken.None)
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
                {
                    personalStatus = MessageStatus.Sent;
                }
                else
                {
                    var readCount = receipts.Count(r => r.Status >= MessageStatus.Read);
                    var deliveredCount = receipts.Count(r => r.Status >= MessageStatus.Delivered);
                    var total = receipts.Count;

                    if (readCount >= total || readCount > 0)
                        personalStatus = MessageStatus.Read;
                    else if (deliveredCount > 0)
                        personalStatus = MessageStatus.Delivered;
                    else
                        personalStatus = MessageStatus.Sent;
                }
            }
            else
            {
                var myReceipt = receipts.FirstOrDefault(r => r.UserId.Value == currentUserIdValue);
                personalStatus = myReceipt?.Status ?? MessageStatus.Sent;
            }

            var deliveredCountDto = receipts.Count(r => r.Status >= MessageStatus.Delivered);
            var readCountDto = receipts.Count(r => r.Status >= MessageStatus.Read);

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

            ReplyInfoDto? replyInfo = null;
            if (m.ReplyToMessageId != null && x.ReplyToSenderId != null)
            {
                var senderName = senderNames.TryGetValue(x.ReplyToSenderId.Value, out var n) ? n : "";
                replyInfo = new ReplyInfoDto
                {
                    MessageId = m.ReplyToMessageId.Value,
                    SenderId = x.ReplyToSenderId.Value,
                    SenderName = senderName,
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
                DeliveredCount = deliveredCountDto,
                ReadCount = readCountDto,
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

        return result;
    }

    public async Task<IReadOnlyList<MessageReadDto>> SearchMessagesAsync(
        RoomId roomId,
        string searchTerm,
        int take,
        CancellationToken ct)
    {
        var term = searchTerm.Trim().ToLower();

        // Search مش بتستخدم AsSplitQuery، ct عادي كويس هنا
        return await _context.Messages
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
}