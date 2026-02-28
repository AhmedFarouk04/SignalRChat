using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class CreateGroupChatHandler : IRequestHandler<CreateGroupChatCommand, ChatRoom>
{
    private readonly IChatRoomRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IMessageRepository _messages;
    private readonly IUserLookupService _users;

    public CreateGroupChatHandler(
        IChatRoomRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBroadcaster broadcaster,
        IMessageRepository messages,
        IUserLookupService users)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _broadcaster = broadcaster;
        _messages = messages;
        _users = users;
    }

    public async Task<ChatRoom> Handle(CreateGroupChatCommand command, CancellationToken ct)
    {
        if (command.CreatorId.Value == Guid.Empty)
            throw new ArgumentException("Creator ID cannot be empty.");

        var trimmedName = command.Name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new ArgumentException("Group name cannot be empty or whitespace.");
        if (trimmedName.Length < 3)
            throw new ArgumentException("Group name must be at least 3 characters long.");
        if (trimmedName.Length > 50)
            throw new ArgumentException("Group name cannot exceed 50 characters.");

        // ✅ فلترة قوية جدًا + رسالة واضحة
        var validMembers = command.Members
            .Where(m => m != Guid.Empty && m != command.CreatorId.Value)
            .Distinct()
            .ToList();

        if (!validMembers.Any())
            throw new ArgumentException("You must select at least one other member to create a group.");

        var room = ChatRoom.CreateGroup(trimmedName, command.CreatorId, validMembers);

        await _repository.AddAsync(room, ct);
        await _unitOfWork.CommitAsync(ct);

        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();
        var creatorName = await _users.GetDisplayNameAsync(command.CreatorId.Value, ct) ?? "Someone";

        var systemText = $"{creatorName} created the group \"{trimmedName}\"";

        var sysMsg = Message.CreateSystemMessage(
            room.Id,
            systemText,
            SystemMessageType.GroupCreated,
            recipients);

        await _messages.AddAsync(sysMsg, ct);
        await _unitOfWork.CommitAsync(ct);

        var msgDto = new MessageDto
        {
            Id = sysMsg.Id.Value,
            RoomId = room.Id.Value,
            SenderId = Guid.Empty,  // System ID
            Content = sysMsg.Content,
            CreatedAt = sysMsg.CreatedAt,
            IsSystemMessage = true
        };
        await _broadcaster.BroadcastMessageAsync(msgDto, recipients);

        await _broadcaster.RoomUpdatedAsync(new RoomUpdatedDto
        {
            RoomId = room.Id.Value,
            MessageId = msgDto.Id,
            SenderId = command.CreatorId.Value,
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
            LastMessageAt = sysMsg.CreatedAt,
            LastMessagePreview = null,  // ✅ من غير Preview
            LastMessageId = sysMsg.Id.Value,
            LastMessageSenderId = Guid.Empty,  // ✅ System ID
            LastMessageStatus = null
        };

        await _broadcaster.RoomUpsertedAsync(dto, recipients);

        return room;
    }
}