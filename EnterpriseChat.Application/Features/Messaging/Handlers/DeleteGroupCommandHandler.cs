using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IMessageBroadcaster? _broadcaster; // optional

    public DeleteGroupCommandHandler(IChatRoomRepository repo, IRoomAuthorizationService auth, IUnitOfWork uow, IMessageBroadcaster? broadcaster)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(DeleteGroupCommand command, CancellationToken ct)
    {
        var room = await _repo.GetByIdWithMembersAsync(command.RoomId, ct)
    ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms can be deleted.");

        await _auth.EnsureUserIsOwnerAsync(command.RoomId, command.RequesterId, ct);

        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();

        await _repo.DeleteAsync(room, ct);
        await _uow.CommitAsync(ct);

        // 1) group deleted لكل الأعضاء
        await _broadcaster.GroupDeletedAsync(room.Id, recipients);

        // 2) شيل الروم من عند كل عضو
        await _broadcaster.RemovedFromRoomAsync(room.Id, recipients);

        return Unit.Value;

    }
}
