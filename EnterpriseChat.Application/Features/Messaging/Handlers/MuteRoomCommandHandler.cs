using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class MuteRoomCommandHandler
    : IRequestHandler<MuteRoomCommand, Unit>
{
    private readonly IMutedRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;

    public MuteRoomCommandHandler(IMutedRoomRepository repo, IUnitOfWork uow, IRoomAuthorizationService auth)
    {
        _repo = repo;
        _uow = uow;
        _auth = auth;
    }

    public async Task<Unit> Handle(MuteRoomCommand command, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.UserId, ct);

        if (await _repo.IsMutedAsync(command.RoomId, command.UserId, ct))
            return Unit.Value;

        await _repo.AddAsync(MutedRoom.Create(command.RoomId, command.UserId), ct);
        await _uow.CommitAsync(ct);

        return Unit.Value;
    }
}
