using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class SendMessageCommandHandler
    : IRequestHandler<SendMessageCommand, MessageDto>
{
    private readonly IChatRoomRepository _roomRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserBlockRepository _blockRepository;
    private readonly IRoomAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBroadcaster? _broadcaster;
    private readonly IUserDirectoryService _userDirectory;
    private readonly IPresenceService _presenceService;
    private readonly IMediator _mediator;                  // جديد
    private readonly IMessageReceiptRepository _receiptRepo;  // جديد
    public SendMessageCommandHandler(
        IChatRoomRepository roomRepository,
        IMessageRepository messageRepository,
        IUserBlockRepository blockRepository,
        IRoomAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IUserDirectoryService userDirectory,
        IPresenceService presenceService,
        IMessageBroadcaster? broadcaster = null,
        IMediator mediator = null!,                        // أضف ده
    IMessageReceiptRepository receiptRepo = null!)
    {
        _roomRepository = roomRepository;
        _messageRepository = messageRepository;
        _blockRepository = blockRepository;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _userDirectory = userDirectory;
        _broadcaster = broadcaster;
        _presenceService = presenceService;
        _mediator = mediator;
        _receiptRepo = receiptRepo;
    }
    

    public async Task<MessageDto> Handle(SendMessageCommand command, CancellationToken ct)
    {
        await _authorization.EnsureUserIsMemberAsync(command.RoomId, command.SenderId, ct);
        var room = await _roomRepository.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Chat room not found.");

        var recipients = room.Members
            .Select(m => m.UserId)
            .Where(id => id != command.SenderId)
            .ToList();

        // 1. فحص الحظر (هل الـ receiver عمل بلوك للـ sender؟)
        bool isBlocked = false;
        if (room.Type == RoomType.Private && recipients.Count == 1)
        {
            var receiverId = recipients[0];
            isBlocked = await _blockRepository.IsBlockedAsync(receiverId, command.SenderId, ct);
            Console.WriteLine($"[Handler] Private room check - receiver={receiverId}, sender={command.SenderId}, isBlockedByReceiver={isBlocked}");
        }

        if (isBlocked)
        {
            recipients = new List<UserId>(); // مفيش receipts ولا بث للمستلم
        }

        // معالجة الرد (Reply)
        bool hasReply = command.ReplyToMessageId != null && command.ReplyToMessageId.Value != Guid.Empty;
        ReplyInfoDto? replyInfo = null;
        if (hasReply && command.ReplyToMessageId != null)
        {
            var repliedMessage = await _messageRepository.GetByIdAsync(command.ReplyToMessageId, ct);
            if (repliedMessage is not null && repliedMessage.RoomId == command.RoomId)
            {
                var sender = await _userDirectory.GetUserAsync(repliedMessage.SenderId, ct);
                replyInfo = new ReplyInfoDto
                {
                    MessageId = repliedMessage.Id.Value,
                    SenderId = repliedMessage.SenderId.Value,
                    SenderName = sender?.DisplayName ?? "User",
                    ContentPreview = repliedMessage.Content.Length > 60 ? repliedMessage.Content[..60] + "…" : repliedMessage.Content,
                    CreatedAt = repliedMessage.CreatedAt,
                    IsDeleted = false
                };
            }
        }

        // 2. إنشاء الرسالة وحفظها
        var message = new Message(
       command.RoomId,
       command.SenderId,
       command.Content,
       recipients,
       command.ReplyToMessageId);

        await _messageRepository.AddAsync(message, ct);
        await _unitOfWork.CommitAsync(ct);
        if (!isBlocked && recipients.Any())
        {
            foreach (var recipientId in recipients)
            {
                var isOnline = await _presenceService.IsOnlineAsync(recipientId);
                if (isOnline)
                {
                    // لو أونلاين، هنعمل Deliver فوري
                    await _mediator.Send(new DeliverMessageCommand(message.Id, recipientId), ct);
                }
            }
        }
        var stats = message.GetReceiptStats();
    

        // ────────────────────────────────────────────────────────────────
        // بعد حفظ الرسالة مباشرة: حدد Delivered لكل مستلم أونلاين
        // ────────────────────────────────────────────────────────────────

        // 1. جيب كل المستخدمين الأونلاين حاليًا (من Redis Presence)
        var allOnlineUsers = await _presenceService.GetOnlineUsersAsync(); // بدون ct
        // 2. فلتر المستلمين اللي أونلاين (من recipients)
        var onlineRecipients = recipients
            .Where(r => allOnlineUsers.Contains(r))
            .ToList();

        // 3. لكل مستلم أونلاين → اعمل Delivered فورًا (من غير ما يفتح الشات)
        foreach (var onlineUser in onlineRecipients)
        {
            // استدعي الـ command أو اعمل broadcast مباشر
            await _mediator.Send(new DeliverMessageCommand(message.Id, onlineUser), ct);
            // أو لو عايز أسرع: broadcast مباشر بدون command
            // await _broadcaster.MessageDeliveredAsync(message.Id.Value, onlineUser.Value);
        }

        // 4. (اختياري) لو عايز تحدث الـ stats للمرسل فورًا بعد الـ Delivered الجديد
        var statsAfterOnline = await _receiptRepo.GetMessageStatsAsync(message.Id, ct);
        await _broadcaster.MessageReceiptStatsUpdatedAsync(
            message.Id.Value,
            command.SenderId.Value,
            statsAfterOnline.TotalRecipients,
            statsAfterOnline.DeliveredCount,
            statsAfterOnline.ReadCount
        );

        Console.WriteLine($"[SEND] Message {message.Id.Value} created with {message.Receipts.Count} receipts");

        var dto = new MessageDto
        {
            Id = message.Id.Value,
            RoomId = command.RoomId.Value,
            SenderId = command.SenderId.Value,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            ReplyInfo = replyInfo,
            ReplyToMessageId = command.ReplyToMessageId?.Value,
            Status = MessageStatus.Sent,
            ReadCount = stats.ReadCount,
            DeliveredCount = stats.DeliveredCount,
            TotalRecipients = stats.TotalRecipients,
            IsEdited = false,
            IsDeleted = false
        };

        // 3. البث
        if (_broadcaster is not null)
        {
            var preview = dto.Content.Length > 80 ? dto.Content[..80] + "…" : dto.Content;

            if (isBlocked)
            {
                Console.WriteLine($"[Handler] Blocked mode - broadcasting only to sender {command.SenderId}");
                var updateForSender = new RoomUpdatedDto
                {
                    RoomId = dto.RoomId,
                    MessageId = dto.Id,
                    SenderId = dto.SenderId,
                    Preview = preview,
                    CreatedAt = dto.CreatedAt,
                    UnreadDelta = 0,
                    IsReply = hasReply,
                    ReplyToMessageId = hasReply ? command.ReplyToMessageId?.Value : null,
                    RoomName = room.Name,
                    RoomType = room.Type.ToString()
                };
                await _broadcaster.RoomUpdatedAsync(updateForSender, new[] { command.SenderId });

                // بث الرسالة للـ sender بس
                await _broadcaster.BroadcastMessageAsync(dto, new[] { command.SenderId });
            }
            else
            {
                // طبيعي: بث للكل
                Console.WriteLine($"[Handler] Normal mode - broadcasting to {recipients.Count} recipients + sender");
                await _broadcaster.BroadcastMessageAsync(dto, recipients.Concat(new[] { command.SenderId }).ToList());

                var updateForRecipients = new RoomUpdatedDto
                {
                    RoomId = dto.RoomId,
                    MessageId = dto.Id,
                    SenderId = dto.SenderId,
                    Preview = preview,
                    CreatedAt = dto.CreatedAt,
                    UnreadDelta = 1,
                    IsReply = hasReply,
                    ReplyToMessageId = hasReply ? command.ReplyToMessageId?.Value : null,
                    RoomName = room.Name,
                    RoomType = room.Type.ToString()
                };
                await _broadcaster.RoomUpdatedAsync(updateForRecipients, recipients);

                var updateForSender = new RoomUpdatedDto
                {
                    RoomId = dto.RoomId,
                    MessageId = dto.Id,
                    SenderId = dto.SenderId,
                    Preview = preview,
                    CreatedAt = dto.CreatedAt,
                    UnreadDelta = 0,
                    IsReply = hasReply,
                    ReplyToMessageId = hasReply ? command.ReplyToMessageId?.Value : null,
                    RoomName = room.Name,
                    RoomType = room.Type.ToString()
                };
                await _broadcaster.RoomUpdatedAsync(updateForSender, new[] { command.SenderId });
            }
        }

        return dto;
    }
}