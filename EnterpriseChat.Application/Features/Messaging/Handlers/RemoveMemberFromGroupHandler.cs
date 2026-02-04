using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class RemoveMemberFromGroupHandler
    : IRequestHandler<RemoveMemberFromGroupCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IMessageRepository _messages;
    private readonly IUserLookupService _users;

    public RemoveMemberFromGroupHandler(
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

    public async Task<Unit> Handle(RemoveMemberFromGroupCommand command, CancellationToken ct)
    {
        if (command.RoomId.Value == Guid.Empty)
            throw new ArgumentException("RoomId is required.");

        if (command.MemberId.Value == Guid.Empty)
            throw new ArgumentException("MemberId is required.");

        var room = await _repo.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms allowed.");

        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.RequesterId, ct);

        if (room.OwnerId == command.MemberId)
            throw new InvalidOperationException("Owner cannot be removed.");

        if (room.Members.Count <= 1)
            throw new InvalidOperationException("Cannot remove the last member of the group.");

        var target = room.Members.FirstOrDefault(m => m.UserId == command.MemberId);
        if (target is null)
            return Unit.Value;

        var requesterIsOwner = room.OwnerId == command.RequesterId;
        if (!requesterIsOwner)
        {
            await _auth.EnsureUserIsAdminAsync(command.RoomId, command.RequesterId, ct);

            if (target.IsAdmin)
                throw new UnauthorizedAccessException("Only owner can remove admins.");
        }

        // ✅ نفّذ الإزالة
        room.RemoveMember(command.MemberId);
        await _uow.CommitAsync(ct);

        // ✅ recipients بعد الإزالة = الأعضاء اللي لسه موجودين
        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();

        // ✅ realtime events (موجودة عندك بالفعل)
        await _broadcaster.MemberRemovedAsync(room.Id, command.MemberId, recipients);

        // ==========================================================
        // ✅ WhatsApp-style: System message persisted + realtime
        // ==========================================================
        var removedName = await _users.GetDisplayNameAsync(command.MemberId.Value, ct)
                         ?? $"User {command.MemberId.Value.ToString()[..8]}";

        var systemSender = new UserId(Guid.Empty);
        var systemText = $"{removedName} was removed from the group";

        // خليها تتسجل عند باقي الأعضاء فقط (اللي اتشال مش هيقدر يشوف الجروب بعد كده)
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

        // 1) ابعت الرسالة داخل الشات لباقي الأعضاء
        await _broadcaster.BroadcastMessageAsync(msgDto, recipients);

        // 2) حدّث ترتيب الغرف + preview بدون unread
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
