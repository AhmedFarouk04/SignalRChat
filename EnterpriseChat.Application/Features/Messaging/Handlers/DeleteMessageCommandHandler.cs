using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class DeleteMessageCommandHandler : IRequestHandler<DeleteMessageCommand, Unit>
{
    private readonly IMessageRepository _messageRepo;
    private readonly IChatRoomRepository _roomRepo;
    private readonly IUnitOfWork _uow;
    private readonly IMessageBroadcaster _broadcaster;

    public DeleteMessageCommandHandler(
        IMessageRepository messageRepo,
        IChatRoomRepository roomRepo,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster)
    {
        _messageRepo = messageRepo;
        _roomRepo = roomRepo;
        _uow = uow;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(DeleteMessageCommand request, CancellationToken ct)
    {
        var message = await _messageRepo.GetByIdAsync(request.MessageId, ct)
                    ?? throw new InvalidOperationException("Message not found.");

        if (request.DeleteForEveryone)
        {
                        var room = await _roomRepo.GetByIdWithMembersAsync(message.RoomId, ct)
                ?? throw new InvalidOperationException("Room not found.");

            bool isSender = message.SenderId == request.UserId;
            bool isAdminOrOwner = room.OwnerId == request.UserId ||
                                  room.Members.Any(m => m.UserId == request.UserId && m.IsAdmin);

            if (!isSender && !isAdminOrOwner)
                throw new UnauthorizedAccessException("You are not allowed to delete this message.");

                        message.DeleteForEveryone(request.UserId);
            await _uow.CommitAsync(ct);

                        var members = await _messageRepo.GetRoomMemberIdsAsync(message.RoomId, ct);

            await _broadcaster.MessageDeletedAsync(
                message.Id,
                isForEveryone: true,
                members);

            await _broadcaster.RoomUpdatedAsync(new RoomUpdatedDto
            {
                RoomId = message.RoomId.Value,
                MessageId = message.Id.Value,
                Preview = "🚫 This message was deleted",
                SenderId = message.SenderId.Value, 
                CreatedAt = DateTime.UtcNow
            }, members);
        }
        else
        {
                        message.DeleteForUser(request.UserId);
            await _uow.CommitAsync(ct);

                        await _broadcaster.MessageDeletedAsync(
                message.Id,
                isForEveryone: false,
                new[] { request.UserId });
        }
        return Unit.Value;
    }

   

}