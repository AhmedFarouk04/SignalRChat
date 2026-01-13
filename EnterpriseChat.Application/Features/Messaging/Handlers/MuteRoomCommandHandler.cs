using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class MuteRoomCommandHandler
{
    private readonly IMutedRoomRepository _repo;
    private readonly IUnitOfWork _uow;

    public MuteRoomCommandHandler(
        IMutedRoomRepository repo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task Handle(
        MuteRoomCommand command,
        CancellationToken ct = default)
    {
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
