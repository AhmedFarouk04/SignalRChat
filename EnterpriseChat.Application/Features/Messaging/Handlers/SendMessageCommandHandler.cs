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

    public SendMessageCommandHandler(
        IChatRoomRepository roomRepository,
        IMessageRepository messageRepository,
        IUserBlockRepository blockRepository,
        IRoomAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IUserDirectoryService userDirectory,
        IMessageBroadcaster? broadcaster = null)
    {
        _roomRepository = roomRepository;
        _messageRepository = messageRepository;
        _blockRepository = blockRepository;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _userDirectory = userDirectory;
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

        if (room.Type == RoomType.Private && recipients.Count == 1)
        {
            var blocked = await _blockRepository.IsBlockedAsync(command.SenderId, recipients[0], ct);
            if (blocked)
                throw new InvalidOperationException("User is blocked.");
        }

        // ✅ الحل النهائي: تحقق من وجود رد باستخدام طريقة آمنة
        bool hasReply = command.ReplyToMessageId != null &&
                       command.ReplyToMessageId.Value != Guid.Empty;

        ReplyInfoDto? replyInfo = null;
        if (hasReply && command.ReplyToMessageId != null)
        {
            // ✅ استخدم Value مباشرة
            var repliedMessage = await _messageRepository.GetByIdAsync(command.ReplyToMessageId, ct);

            if (repliedMessage is null || repliedMessage.RoomId != command.RoomId)
                throw new InvalidOperationException("Cannot reply to a message in a different room or non-existent message.");

            // إنشاء ReplyInfo
            var sender = await _userDirectory.GetUserAsync(repliedMessage.SenderId, ct);

            // ✅ استخدم Value مباشرة
            replyInfo = new ReplyInfoDto
            {
                MessageId = repliedMessage.Id.Value,
                SenderId = repliedMessage.SenderId.Value,
                SenderName = sender?.DisplayName ?? "User",
                ContentPreview = repliedMessage.Content.Length > 60
                    ? repliedMessage.Content[..60] + "…"
                    : repliedMessage.Content,
                CreatedAt = repliedMessage.CreatedAt,
                IsDeleted = false
            };
        }

        // إنشاء الرسالة
        var message = new Message(
            command.RoomId,
            command.SenderId,
            command.Content,
            recipients,
            command.ReplyToMessageId);

        await _messageRepository.AddAsync(message, ct);
        await _unitOfWork.CommitAsync(ct);

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
            ReadCount = 0,
            DeliveredCount = 0,
            TotalRecipients = recipients.Count,
            IsEdited = false,
            IsDeleted = false
        };

        if (_broadcaster is not null)
        {
            // 1) ابعت الرسالة الجديدة للـ recipients
            await _broadcaster.BroadcastMessageAsync(dto, recipients);

            // 2) ابعت RoomUpdated للـ recipients (+1 unread)
            var preview = dto.Content.Length > 80 ? dto.Content[..80] + "…" : dto.Content;

            var updateForRecipients = new RoomUpdatedDto
            {
                RoomId = dto.RoomId,
                MessageId = dto.Id,
                SenderId = dto.SenderId,
                Preview = preview,
                CreatedAt = dto.CreatedAt,
                UnreadDelta = 1,
                IsReply = hasReply,
                ReplyToMessageId = hasReply ? command.ReplyToMessageId?.Value : null
            };
            await _broadcaster.RoomUpdatedAsync(updateForRecipients, recipients);

            // 3) ابعت RoomUpdated للـ sender (+0 unread)
            var updateForSender = new RoomUpdatedDto
            {
                RoomId = dto.RoomId,
                MessageId = dto.Id,
                SenderId = dto.SenderId,
                Preview = preview,
                CreatedAt = dto.CreatedAt,
                UnreadDelta = 0,
                IsReply = hasReply,
                ReplyToMessageId = hasReply ? command.ReplyToMessageId?.Value : null
            };
            await _broadcaster.RoomUpdatedAsync(updateForSender, new[] { command.SenderId });
        }

        return dto;
    }
}