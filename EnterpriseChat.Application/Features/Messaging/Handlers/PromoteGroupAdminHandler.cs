using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class PromoteGroupAdminHandler
    : IRequestHandler<PromoteGroupAdminCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IMessageBroadcaster _broadcaster;
    public PromoteGroupAdminHandler(
        IChatRoomRepository repo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(PromoteGroupAdminCommand command, CancellationToken ct)
    {
        if (command.RoomId.Value == Guid.Empty)
            throw new ArgumentException("RoomId is required.");

        if (command.TargetUserId.Value == Guid.Empty)
            throw new ArgumentException("TargetUserId is required.");

        // Owner only
        await _auth.EnsureUserIsOwnerAsync(command.RoomId, command.RequesterId, ct);

        var room = await _repo.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms allowed.");

        if (room.OwnerId != null && room.OwnerId.Value == command.TargetUserId.Value)
            return Unit.Value;

        var member = room.Members.FirstOrDefault(m => m.UserId.Value == command.TargetUserId.Value);
        if (member is null)
            throw new InvalidOperationException("Target user is not a member of this group.");

        if (member.IsAdmin)
            return Unit.Value;

        member.PromoteToAdmin();
        await _uow.CommitAsync(ct);

        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();
        await _broadcaster.AdminPromotedAsync(room.Id, command.TargetUserId, recipients);

        return Unit.Value;

    }
}
