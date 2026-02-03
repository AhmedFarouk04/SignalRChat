using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed class LeaveGroupCommandHandler : IRequestHandler<LeaveGroupCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;

    public LeaveGroupCommandHandler(IChatRoomRepository repo, IRoomAuthorizationService auth, IUnitOfWork uow)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
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

        return Unit.Value;
    }
}
