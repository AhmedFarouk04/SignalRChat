using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class AddMemberToGroupHandler : IRequestHandler<AddMemberToGroupCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IUserBlockRepository _blocks;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IUserLookupService _users;
    private readonly IMessageRepository _messages;

    public AddMemberToGroupHandler(
        IChatRoomRepository repo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IUserBlockRepository blocks,
        IMessageBroadcaster broadcaster,
        IUserLookupService users,
        IMessageRepository messages)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
        _blocks = blocks;
        _broadcaster = broadcaster;
        _users = users;
        _messages = messages;
    }

    public async Task<Unit> Handle(AddMemberToGroupCommand command, CancellationToken ct)
    {
        if (command.RoomId.Value == Guid.Empty) throw new ArgumentException("RoomId is required.");
        if (command.MemberId.Value == Guid.Empty) throw new ArgumentException("MemberId is required.");

        await _auth.EnsureUserIsAdminAsync(command.RoomId, command.RequesterId, ct);

        var room = await _repo.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms allowed.");

        if (await _blocks.IsBlockedAsync(command.RequesterId, command.MemberId, ct))
            throw new InvalidOperationException("Cannot add this user due to blocking.");

        if (room.Members.Any(m => m.UserId.Value == command.MemberId.Value))
            throw new InvalidOperationException("User is already a member of this group.");

        room.AddMember(command.MemberId);

        try
        {
            await _uow.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Unit.Value; // duplicate race => success
        }

        // ✅ realtime: العضو الجديد يشوف الجروب فورًا
        var now = DateTime.UtcNow;

        var roomDtoForNewMember = new RoomListItemDto
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

        await _broadcaster.RoomUpsertedAsync(roomDtoForNewMember, new[] { command.MemberId });

        // ✅ الاسم
        var displayName = await _users.GetDisplayNameAsync(command.MemberId.Value, ct);


        // realtime for new member room upsert
        // بعد Commit الأول (add member)
        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();

        var addedName = await _users.GetDisplayNameAsync(command.MemberId.Value, ct) ?? "Someone";
        var requesterName = await _users.GetDisplayNameAsync(command.RequesterId.Value, ct) ?? "Someone";

        var systemSender = command.RequesterId; // الأفضل
        var systemText = $"{addedName} was added by {requesterName}";

        var sysMsg = new Message(room.Id, systemSender, systemText, recipients);
        await _messages.AddAsync(sysMsg, ct);
        await _uow.CommitAsync(ct);

        var msgDto = new MessageDto
        {
            Id = sysMsg.Id.Value,
            RoomId = room.Id.Value,
            SenderId = command.RequesterId.Value,
            Content = systemText,
            CreatedAt = sysMsg.CreatedAt
        };

        await _broadcaster.BroadcastMessageAsync(msgDto, recipients);

        var preview = systemText.Length > 80 ? systemText[..80] + "…" : systemText;
        await _broadcaster.RoomUpdatedAsync(new RoomUpdatedDto
        {
            RoomId = msgDto.RoomId,
            MessageId = msgDto.Id,
            SenderId = command.RequesterId.Value,
            Preview = preview,
            CreatedAt = msgDto.CreatedAt,
            UnreadDelta = 0
        }, recipients);

        // optional event
        await _broadcaster.MemberAddedAsync(room.Id, command.MemberId, addedName, recipients);

        return Unit.Value;
    }
}
