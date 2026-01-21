using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class DemoteGroupAdminHandler
    : IRequestHandler<DemoteGroupAdminCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;

    public DemoteGroupAdminHandler(
        IChatRoomRepository repo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
    }

    public async Task<Unit> Handle(DemoteGroupAdminCommand command, CancellationToken ct)
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

        // Owner لا يمكن تنزيله
        if (room.OwnerId != null && room.OwnerId.Value == command.TargetUserId.Value)
            throw new InvalidOperationException("Owner cannot be demoted.");

        var member = room.Members.FirstOrDefault(m => m.UserId.Value == command.TargetUserId.Value);
        if (member is null)
            throw new InvalidOperationException("Target user is not a member of this group.");

        if (!member.IsAdmin)
            return Unit.Value;

        member.DemoteFromAdmin();

        await _uow.CommitAsync(ct);
        return Unit.Value;
    }
}
