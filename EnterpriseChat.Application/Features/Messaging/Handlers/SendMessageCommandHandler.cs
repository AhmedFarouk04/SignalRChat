using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
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
    private readonly IMessageBroadcaster? _broadcaster; // optional

    public SendMessageCommandHandler(
        IChatRoomRepository roomRepository,
        IMessageRepository messageRepository,
        IUserBlockRepository blockRepository,
        IRoomAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IMessageBroadcaster? broadcaster = null)
    {
        _roomRepository = roomRepository;
        _messageRepository = messageRepository;
        _blockRepository = blockRepository;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
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

        var message = new Message(command.RoomId, command.SenderId, command.Content, recipients);

        await _messageRepository.AddAsync(message, ct);
        await _unitOfWork.CommitAsync(ct);

        var dto = new MessageDto
        {
            Id = message.Id.Value,
            RoomId = command.RoomId.Value,
            SenderId = command.SenderId.Value,
            Content = message.Content,
            CreatedAt = message.CreatedAt
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
                UnreadDelta = 1
            };
            await _broadcaster.RoomUpdatedAsync(updateForRecipients, recipients);

            // 3) ابعت RoomUpdated للـ sender (+0 unread) عشان preview وترتيب القائمة يتحدث
            var updateForSender = new RoomUpdatedDto
            {
                RoomId = dto.RoomId,
                MessageId = dto.Id,
                SenderId = dto.SenderId,
                Preview = preview,
                CreatedAt = dto.CreatedAt,
                UnreadDelta = 0
            };
            await _broadcaster.RoomUpdatedAsync(updateForSender, new[] { command.SenderId });
        }

        return dto;
    }

}