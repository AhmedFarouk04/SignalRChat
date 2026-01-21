using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class UnmuteRoomCommandHandler
{
    private readonly IMutedRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;


    public UnmuteRoomCommandHandler(
        IMutedRoomRepository repo,
        IUnitOfWork uow,
        IRoomAuthorizationService auth)
    {
        _repo = repo;
        _uow = uow;
        _auth = auth;
    }

    public async Task Handle(
        UnmuteRoomCommand command,
        CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(
    command.RoomId,
    command.UserId,
    ct);

        await _repo.RemoveAsync(command.RoomId, command.UserId);
        await _uow.CommitAsync(ct);
    }
}
