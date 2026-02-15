// ✅ SendMessageCommandHandler.cs - النسخة المُصلحة

using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, MessageDto>
{
    private readonly IChatRoomRepository _roomRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserBlockRepository _blockRepository;
    private readonly IRoomAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBroadcaster? _broadcaster;
    private readonly IUserDirectoryService _userDirectory;
    private readonly IPresenceService _presenceService;
    private readonly IMediator _mediator;
    private readonly IMessageReceiptRepository _receiptRepo;

    public SendMessageCommandHandler(
        IChatRoomRepository roomRepository,
        IMessageRepository messageRepository,
        IUserBlockRepository blockRepository,
        IRoomAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IUserDirectoryService userDirectory,
        IPresenceService presenceService,
        IMediator mediator,
        IMessageReceiptRepository receiptRepo,
        IMessageBroadcaster? broadcaster = null)
    {
        _roomRepository = roomRepository;
        _messageRepository = messageRepository;
        _blockRepository = blockRepository;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _userDirectory = userDirectory;
        _presenceService = presenceService;
        _mediator = mediator;
        _receiptRepo = receiptRepo;
        _broadcaster = broadcaster;
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

        // 1. فحص الحظر
        bool isBlocked = false;
        if (room.Type == RoomType.Private && recipients.Count == 1)
        {
            var receiverId = recipients[0];
            isBlocked = await _blockRepository.IsBlockedAsync(receiverId, command.SenderId, ct);
        }

        if (isBlocked) recipients = new List<UserId>();

        // 2. معالجة الرد (Reply)
        ReplyInfoDto? replyInfo = null;
        if (command.ReplyToMessageId != null && command.ReplyToMessageId.Value != Guid.Empty)
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

        // 3. إنشاء الرسالة وحفظها
        var message = new Message(
            command.RoomId,
            command.SenderId,
            command.Content,
            recipients,
            command.ReplyToMessageId);

        await _messageRepository.AddAsync(message, ct);

        // 🔥 الخطوة 1: حفظ الرسالة والـ Receipts
        await _unitOfWork.CommitAsync(ct);

        // 🔥 الخطوة 2: عمل Deliver للأونلاين فوراً
        var onlineUsers = await _presenceService.GetOnlineUsersAsync();
        var toDeliverImmediately = recipients
            .Where(r => onlineUsers.Any(o => o.Value == r.Value))
            .ToList();

        foreach (var userId in toDeliverImmediately)
        {
            await _mediator.Send(new DeliverMessageCommand(message.Id, userId), ct);
        }

        // 🔥 الخطوة 3: Commit الـ Deliver
        await _unitOfWork.CommitAsync(ct);

        // 🔥 الخطوة 4: جلب الـ Stats النهائية
        var finalStats = await _receiptRepo.GetMessageStatsAsync(message.Id, ct);

        // 🔥 الخطوة 5: تجهيز الـ DTO
        var dto = new MessageDto
        {
            Id = message.Id.Value,
            RoomId = command.RoomId.Value,
            SenderId = command.SenderId.Value,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            ReplyInfo = replyInfo,
            ReplyToMessageId = command.ReplyToMessageId?.Value,
            Status = finalStats.ReadCount >= finalStats.TotalRecipients ? MessageStatus.Read :
                     finalStats.DeliveredCount > 0 ? MessageStatus.Delivered : MessageStatus.Sent,
            ReadCount = finalStats.ReadCount,
            DeliveredCount = finalStats.DeliveredCount,
            TotalRecipients = finalStats.TotalRecipients,
            IsEdited = false,
            IsDeleted = false
        };

        // 🔥 الخطوة 6: Broadcasting (بعد ما كل حاجة تمت بنجاح)
        if (_broadcaster is not null)
        {
            try
            {
                var preview = dto.Content.Length > 80 ? dto.Content[..80] + "…" : dto.Content;

                // بث الرسالة
                var broadcastTargets = isBlocked
    ? new List<UserId> { command.SenderId }  // ✅ List
    : recipients.Concat(new[] { command.SenderId }).ToList();

                await _broadcaster.BroadcastMessageAsync(dto, broadcastTargets);

                // تحديث القائمة الجانبية
                var updateDto = new RoomUpdatedDto
                {
                    RoomId = dto.RoomId,
                    MessageId = dto.Id,
                    SenderId = dto.SenderId,
                    Preview = preview,
                    CreatedAt = dto.CreatedAt,
                    UnreadDelta = 1,
                    IsReply = dto.ReplyToMessageId.HasValue,
                    RoomName = room.Name,
                    RoomType = room.Type.ToString()
                };

                await _broadcaster.RoomUpdatedAsync(updateDto, recipients);

                updateDto.UnreadDelta = 0;
                await _broadcaster.RoomUpdatedAsync(updateDto, new[] { command.SenderId });

                // تحديث الـ Stats
                await _broadcaster.MessageReceiptStatsUpdatedAsync(
                    dto.Id,
                    command.RoomId.Value,
                    finalStats.TotalRecipients,
                    finalStats.DeliveredCount,
                    finalStats.ReadCount);
            }
            catch (Exception ex)
            {
                // ✅ Log الـ error بس متمنعش الـ response
                Console.WriteLine($"[Broadcasting Error] {ex.Message}");
            }
        }

        return dto;
    }
}