using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class RemoveMemberFromGroupHandler
    : IRequestHandler<RemoveMemberFromGroupCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;

    public RemoveMemberFromGroupHandler(
        IChatRoomRepository repo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
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

        room.RemoveMember(command.MemberId);
        await _uow.CommitAsync(ct);

        return Unit.Value;
    }
}
