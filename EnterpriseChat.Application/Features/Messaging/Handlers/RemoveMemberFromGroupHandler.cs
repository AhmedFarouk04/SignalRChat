using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class RemoveMemberFromGroupHandler
{
    private readonly IChatRoomRepository _repo;
    private readonly IUnitOfWork _uow;

    public RemoveMemberFromGroupHandler(
        IChatRoomRepository repo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task Handle(
        RemoveMemberFromGroupCommand command,
        CancellationToken ct = default)
    {
        var room = await _repo.GetByIdAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms allowed.");

        if (room.OwnerId != command.RequesterId)
            throw new UnauthorizedAccessException("Only owner can remove members.");

        if (command.MemberId == room.OwnerId)
            throw new InvalidOperationException("Owner cannot be removed.");

        room.RemoveMember(command.MemberId);

        await _uow.CommitAsync(ct);
    }
}
