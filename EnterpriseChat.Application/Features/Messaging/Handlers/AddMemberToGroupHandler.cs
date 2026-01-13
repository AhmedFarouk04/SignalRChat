using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class AddMemberToGroupHandler
{
    private readonly IChatRoomRepository _repo;
    private readonly IUnitOfWork _uow;

    public AddMemberToGroupHandler(
        IChatRoomRepository repo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task Handle(
        AddMemberToGroupCommand command,
        CancellationToken ct = default)
    {
        var room = await _repo.GetByIdAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms allowed.");

        if (room.OwnerId != command.RequesterId)
            throw new UnauthorizedAccessException("Only owner can add members.");

        room.AddMember(command.MemberId);

        await _uow.CommitAsync(ct);
    }
}
