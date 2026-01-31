using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class RenameGroupCommandHandler
    : IRequestHandler<RenameGroupCommand, Unit>
{
    private readonly IChatRoomRepository _rooms;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;

    public RenameGroupCommandHandler(IChatRoomRepository rooms, IRoomAuthorizationService auth, IUnitOfWork uow)
    {
        _rooms = rooms;
        _auth = auth;
        _uow = uow;
    }

    public async Task<Unit> Handle(RenameGroupCommand request, CancellationToken ct)
    {
        await _auth.EnsureUserIsAdminAsync(request.RoomId, request.RequesterId, ct);

        var room = await _rooms.GetByIdAsync(request.RoomId, ct)
            ?? throw new KeyNotFoundException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms can be renamed.");

        room.Rename(request.Name);

        await _uow.CommitAsync(ct);
        return Unit.Value;
    }
}
