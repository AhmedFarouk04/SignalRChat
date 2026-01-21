using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class MuteRoomCommandHandler
{
    private readonly IMutedRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;

    public MuteRoomCommandHandler(
        IMutedRoomRepository repo,
        IUnitOfWork uow,
        IRoomAuthorizationService auth)
    {
        _repo = repo;
        _uow = uow;
        _auth = auth;
    }

    public async Task Handle(
        MuteRoomCommand command,
        CancellationToken ct = default)
    {

        await _auth.EnsureUserIsMemberAsync(
    command.RoomId,
    command.UserId,
    ct);

        var isMuted =
            await _repo.IsMutedAsync(
                command.RoomId,
                command.UserId,
                ct);

        if (isMuted)
            return;

        var mute = MutedRoom.Create(
            command.RoomId,
            command.UserId);

        await _repo.AddAsync(mute, ct);
        await _uow.CommitAsync(ct);
    }
}
