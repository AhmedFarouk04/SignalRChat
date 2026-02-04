using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

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

        // ✅ خروج العضو
        room.RemoveMember(command.RequesterId);
        await _uow.CommitAsync(ct);

        // ✅ recipients بعد الخروج = باقي الأعضاء
        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();

        // ✅ استخدم الموجود عندك بدل MemberLeftAsync/RemovedFromRoomAsync
        // هيوصل event "MemberRemoved" للباقي + "RemovedFromRoom" للي خرج (من SignalRMessageBroadcaster)
        await _broadcaster.MemberRemovedAsync(room.Id, command.RequesterId, recipients);

        // ==========================================================
        // ✅ WhatsApp-style: System message persisted + realtime
        // ==========================================================
        var leaverName = await _users.GetDisplayNameAsync(command.RequesterId.Value, ct)
                        ?? $"User {command.RequesterId.Value.ToString()[..8]}";

        var systemSender = new UserId(Guid.Empty);
        var systemText = $"{leaverName} left the group";

        var sysMsg = new Message(room.Id, systemSender, systemText, recipients);
        await _messages.AddAsync(sysMsg, ct);
        await _uow.CommitAsync(ct);

        var msgDto = new MessageDto
        {
            Id = sysMsg.Id.Value,
            RoomId = room.Id.Value,
            SenderId = Guid.Empty,
            Content = sysMsg.Content,
            CreatedAt = sysMsg.CreatedAt
        };

        // 1) الرسالة تظهر داخل الشات عند باقي الأعضاء
        await _broadcaster.BroadcastMessageAsync(msgDto, recipients);

        // 2) تحديث ترتيب الغرف + preview بدون unread
        var preview = msgDto.Content.Length > 80 ? msgDto.Content[..80] + "…" : msgDto.Content;
        await _broadcaster.RoomUpdatedAsync(new RoomUpdatedDto
        {
            RoomId = msgDto.RoomId,
            MessageId = msgDto.Id,
            SenderId = msgDto.SenderId,
            Preview = preview,
            CreatedAt = msgDto.CreatedAt,
            UnreadDelta = 0
        }, recipients);

        return Unit.Value;
    }
}
