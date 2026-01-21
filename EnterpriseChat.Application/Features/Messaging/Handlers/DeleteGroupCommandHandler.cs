using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class DeleteGroupCommandHandler
    : IRequestHandler<DeleteGroupCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;

    public DeleteGroupCommandHandler(
        IChatRoomRepository repo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
    }

    public async Task<Unit> Handle(DeleteGroupCommand command, CancellationToken ct)
    {
        // Owner only
        await _auth.EnsureUserIsOwnerAsync(
            command.RoomId,
            command.RequesterId,
            ct);

        var room = await _repo.GetByIdAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms can be deleted.");

        await _repo.DeleteAsync(room, ct);
        await _uow.CommitAsync(ct);

        return Unit.Value;
    }
}
