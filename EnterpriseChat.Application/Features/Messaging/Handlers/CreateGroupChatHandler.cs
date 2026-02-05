using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class CreateGroupChatHandler
    : IRequestHandler<CreateGroupChatCommand, ChatRoom>
{
    private readonly IChatRoomRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IMessageRepository _messages;
    private readonly IUserLookupService _users;

    public CreateGroupChatHandler(
        IChatRoomRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBroadcaster broadcaster, IMessageRepository message, IUserLookupService users)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _broadcaster = broadcaster;
        _messages = message;
        _users = users;

    }

    public async Task<ChatRoom> Handle(CreateGroupChatCommand command, CancellationToken ct)
    {
        var validMembers = command.Members.Where(m => m.Value != Guid.Empty).ToList();
        var room = ChatRoom.CreateGroup(command.Name, command.CreatorId, validMembers);
        await _repository.AddAsync(room, ct);
        await _unitOfWork.CommitAsync(ct);  // ✅ انقل هذا هنا أولاً!

        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();
        var creatorName = await _users.GetDisplayNameAsync(command.CreatorId.Value, ct) ?? "Someone";

        // ✅ استخدم command.CreatorId بدلاً من Guid.Empty
        var sysMsg = new Message(room.Id, command.CreatorId, $"{creatorName} created the group", recipients);

        await _messages.AddAsync(sysMsg, ct);
        await _unitOfWork.CommitAsync(ct);

        var msgDto = new MessageDto
        {
            Id = sysMsg.Id.Value,
            RoomId = room.Id.Value,
            SenderId = command.CreatorId.Value,  // ✅ هنا أيضاً
            Content = $"{creatorName} created the group",
            CreatedAt = sysMsg.CreatedAt
        };

        await _broadcaster.BroadcastMessageAsync(msgDto, recipients);

        await _broadcaster.RoomUpdatedAsync(new RoomUpdatedDto
        {
            RoomId = room.Id.Value,
            MessageId = msgDto.Id,
            SenderId = command.CreatorId.Value,  // ✅ وهنا أيضاً
            Preview = "Group created",
            CreatedAt = msgDto.CreatedAt,
            UnreadDelta = 0
        }, recipients);

        var now = DateTime.UtcNow;

        var dto = new RoomListItemDto
        {
            Id = room.Id.Value,
            Name = room.Name,
            Type = room.Type.ToString(),
            UnreadCount = 0,
            IsMuted = false,
            LastMessageAt = now,
            LastMessagePreview = null,
            LastMessageId = null,
            LastMessageSenderId = null,
            LastMessageStatus = null
        };

        await _broadcaster.RoomUpsertedAsync(dto, recipients);

        return room;
    }
}
