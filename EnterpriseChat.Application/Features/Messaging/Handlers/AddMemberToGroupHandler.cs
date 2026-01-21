using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class AddMemberToGroupHandler : IRequestHandler<AddMemberToGroupCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;

    public AddMemberToGroupHandler(
        IChatRoomRepository repo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
    }

    public async Task<Unit> Handle(AddMemberToGroupCommand command, CancellationToken ct)
    {
        if (command.RoomId.Value == Guid.Empty)
            throw new ArgumentException("RoomId is required.");

        if (command.MemberId.Value == Guid.Empty)
            throw new ArgumentException("MemberId is required.");

        await _auth.EnsureUserIsAdminAsync(command.RoomId, command.RequesterId, ct);

        var room = await _repo.GetByIdWithMembersAsync(command.RoomId, ct)
           ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms allowed.");

        room.AddMember(command.MemberId);

        await _uow.CommitAsync(ct);

        return Unit.Value;
    }
}
