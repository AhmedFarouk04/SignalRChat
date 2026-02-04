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

    public CreateGroupChatHandler(
        IChatRoomRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBroadcaster broadcaster)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _broadcaster = broadcaster;
    }

    public async Task<ChatRoom> Handle(CreateGroupChatCommand command, CancellationToken ct)
    {
        var room = ChatRoom.CreateGroup(command.Name, command.CreatorId, command.Members);

        await _repository.AddAsync(room, ct);
        await _unitOfWork.CommitAsync(ct);

        // ✅ realtime room يظهر فورًا ويتحط فوق
        var now = DateTime.UtcNow;

        var dto = new RoomListItemDto
        {
            Id = room.Id.Value,
            Name = room.Name,
            Type = room.Type.ToString(),
            UnreadCount = 0,
            IsMuted = false,
            LastMessageAt = now,              // ✅ يخليه أول القائمة
            LastMessagePreview = null,
            LastMessageId = null,
            LastMessageSenderId = null,
            LastMessageStatus = null
        };

        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();
        await _broadcaster.RoomUpsertedAsync(dto, recipients);


        return room;
    }
}
