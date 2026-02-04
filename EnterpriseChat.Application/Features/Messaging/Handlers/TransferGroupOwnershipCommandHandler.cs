using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class TransferGroupOwnershipCommandHandler
    : IRequestHandler<TransferGroupOwnershipCommand, Unit>
{
    private readonly IChatRoomRepository _rooms;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IMessageBroadcaster _broadcaster;

    public TransferGroupOwnershipCommandHandler(
        IChatRoomRepository rooms,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster)
    {
        _rooms = rooms;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(TransferGroupOwnershipCommand request, CancellationToken ct)
    {
        await _auth.EnsureUserIsOwnerAsync(request.RoomId, request.RequesterId, ct);

        var room = await _rooms.GetByIdWithMembersAsync(request.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Ownership transfer is only allowed for group rooms.");

        if (!room.IsMember(request.NewOwnerId))
            throw new InvalidOperationException("New owner must be a member of the group.");

        if (room.OwnerId != null && room.OwnerId.Value == request.NewOwnerId.Value)
            return Unit.Value;

        room.SetOwner(request.NewOwnerId);

        var newOwnerMember = room.Members.First(m => m.UserId.Value == request.NewOwnerId.Value);
        newOwnerMember.PromoteToAdmin();


        await _uow.CommitAsync(ct);

        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();

        await _broadcaster.OwnerTransferredAsync(room.Id, request.NewOwnerId, recipients);

        // اختياري (لو الواجهة بتتابع AdminPromoted):
        await _broadcaster.AdminPromotedAsync(room.Id, request.NewOwnerId, recipients);

        return Unit.Value;

    }
}
