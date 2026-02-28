using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

public sealed class AddMembersToGroupBulkHandler : IRequestHandler<AddMembersToGroupBulkCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IUserBlockRepository _blocks;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IUserLookupService _users;
    private readonly IMessageRepository _messages;

    public AddMembersToGroupBulkHandler(IChatRoomRepository repo,
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

    public async Task<Unit> Handle(AddMembersToGroupBulkCommand command, CancellationToken ct)
    {
        await _auth.EnsureUserIsAdminAsync(command.RoomId, command.RequesterId, ct);

        var room = await _repo.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        var requesterName = await _users.GetDisplayNameAsync(command.RequesterId.Value, ct) ?? "Someone";

        var existingMemberIds = room.Members.Select(m => m.UserId.Value).ToHashSet();
        var addedMembersInfos = new List<(UserId Id, string Name)>();

        // ✅ فلترة قوية + رسالة واضحة لو مفيش أعضاء صالحين
        foreach (var memberId in command.MemberIds.Distinct())
        {
            if (memberId.Value == Guid.Empty) continue; // تجاهل Empty
            if (existingMemberIds.Contains(memberId.Value)) continue;
            if (await _blocks.IsBlockedAsync(command.RequesterId, memberId, ct)) continue;

            var name = await _users.GetDisplayNameAsync(memberId.Value, ct) ?? "Unknown User";

            room.AddMember(memberId);
            addedMembersInfos.Add((memberId, name));
        }

        if (!addedMembersInfos.Any())
            throw new ArgumentException("No valid users to add. All users are already members or blocked.");

        await _uow.CommitAsync(ct);

        foreach (var info in addedMembersInfos)
        {
            var roomDto = new RoomListItemDto
            {
                Id = room.Id.Value,
                Name = room.Name,
                Type = room.Type.ToString(),
                LastMessageAt = DateTime.UtcNow
            };
            await _broadcaster.RoomUpsertedAsync(roomDto, new[] { info.Id });
        }

        var addedNames = string.Join(", ", addedMembersInfos.Select(x => x.Name));
        var systemText = $"{addedNames} were added by {requesterName}";

        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();

        var sysMsg = Message.CreateSystemMessage(
            room.Id,
            systemText,
            SystemMessageType.MemberAdded,
            recipients);

        await _messages.AddAsync(sysMsg, ct);
        await _uow.CommitAsync(ct);

        await NotifyGroup(room, sysMsg, systemText, recipients, addedMembersInfos);

        return Unit.Value;
    }



    private async Task NotifyGroup(ChatRoom room, Message sysMsg, string text,
    List<UserId> recipients, List<(UserId Id, string Name)> addedMembers)
    {
        var msgDto = new MessageDto
        {
            Id = sysMsg.Id.Value,
            RoomId = room.Id.Value,
            Content = text,
            CreatedAt = sysMsg.CreatedAt,
            IsSystemMessage = true  // ✅ مهم جداً
        };

        // 1. بث الرسالة داخل الشات (بتظهر جوه المحادثة)
        await _broadcaster.BroadcastMessageAsync(msgDto, recipients);

        // 2. تحديث القائمة بدون Preview (زي ما عملنا في RemoveMember)
        await _broadcaster.RoomUpsertedAsync(new RoomListItemDto
        {
            Id = room.Id.Value,
            Name = room.Name,
            Type = room.Type.ToString(),
            UnreadCount = 0,
            IsMuted = false,
            LastMessageAt = sysMsg.CreatedAt,
            LastMessagePreview = null,  // ✅ صح
            LastMessageId = sysMsg.Id.Value,
            LastMessageSenderId = Guid.Empty,  // ✅ صح
            LastMessageStatus = null
        }, recipients);

        // 3. إشعارات للأعضاء الجدد (حالياً مش محتاجين RoomUpdated)
        var addedIds = addedMembers.Select(x => x.Id.Value).ToHashSet();
        var existingMembers = recipients.Where(r => !addedIds.Contains(r.Value)).ToList();

        foreach (var info in addedMembers)
        {
            await _broadcaster.MemberAddedAsync(room.Id, info.Id, info.Name, existingMembers);
        }
    }
}