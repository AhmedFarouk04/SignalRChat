using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;
namespace EnterpriseChat.Application.Features.Messaging.Handlers;
public sealed class DeleteChatCommandHandler
    : IRequestHandler<DeleteChatCommand, Unit>
{
    private readonly IChatRoomRepository _roomRepo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IMessageBroadcaster _broadcaster;
    public DeleteChatCommandHandler(
        IChatRoomRepository roomRepo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster)
    {
        _roomRepo = roomRepo;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
    }
    public async Task<Unit> Handle(DeleteChatCommand command, CancellationToken ct)
    {
        Console.WriteLine($"[DeleteChat] RoomId={command.RoomId}, RequesterId={command.RequesterId}");

        var room = await _roomRepo.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");
        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.RequesterId, ct);
        if (room.Type == RoomType.Group)
        {
                        if (room.OwnerId != command.RequesterId)
                throw new UnauthorizedAccessException("Only owner can delete a group.");
            var recipients = room.GetMemberIds().ToList();
            await _roomRepo.DeleteAsync(room, ct);
            await _uow.CommitAsync(ct);
            await _broadcaster.GroupDeletedAsync(room.Id, recipients);
            await _broadcaster.RemovedFromRoomAsync(room.Id, recipients);
        }
        else
        {
                                    room.ClearChatForMember(command.RequesterId);
            room.DeleteForMember(command.RequesterId);
            await _uow.CommitAsync(ct);
                        await _broadcaster.ChatDeletedForUserAsync(room.Id, command.RequesterId);
        }
        return Unit.Value;
    }
}