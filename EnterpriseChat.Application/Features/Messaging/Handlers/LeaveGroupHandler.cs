using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class LeaveGroupCommandHandler : IRequestHandler<LeaveGroupCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IMessageRepository _messages;
    private readonly IUserLookupService _users;

    public LeaveGroupCommandHandler(
        IChatRoomRepository repo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster,
        IMessageRepository messages,
        IUserLookupService users)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
        _messages = messages;
        _users = users;
    }

    public async Task<Unit> Handle(LeaveGroupCommand command, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.RequesterId, ct);

        var room = await _repo.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms allowed.");

        if (room.OwnerId == command.RequesterId)
            throw new InvalidOperationException("Owner cannot leave the group. Transfer ownership first.");

        if (room.Members.Count == 1)
            throw new InvalidOperationException("Cannot remove the last member of the group.");

        room.RemoveMember(command.RequesterId);
        await _uow.CommitAsync(ct);

        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();
        var leaverName = await _users.GetDisplayNameAsync(command.RequesterId.Value, ct) ?? "Someone";

        await _broadcaster.MemberRemovedAsync(room.Id, command.RequesterId, command.RequesterId, leaverName, recipients);

        var systemText = $"{leaverName} left the group";

        var sysMsg = Message.CreateSystemMessage(
            room.Id,
            systemText,
            SystemMessageType.MemberRemoved,
            recipients);

        await _messages.AddAsync(sysMsg, ct);
        await _uow.CommitAsync(ct);

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

        var preview = msgDto.Content.Length > 80 ? msgDto.Content[..80] + "…" : msgDto.Content;
        await _broadcaster.RoomUpsertedAsync(new RoomListItemDto
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
        }, recipients);


        return Unit.Value;
    }
}