using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class ClearChatCommandHandler
    : IRequestHandler<ClearChatCommand, Unit>
{
    private readonly IChatRoomRepository _roomRepo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IMessageBroadcaster _broadcaster;

    public ClearChatCommandHandler(
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

    public async Task<Unit> Handle(ClearChatCommand command, CancellationToken ct)
    {
        var room = await _roomRepo.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.RequesterId, ct);

        if (command.ForEveryone)
        {
            if (room.Type != RoomType.Group)
                throw new InvalidOperationException(
                    "Clear for everyone is only available in groups.");

            if (room.OwnerId != command.RequesterId)
                throw new UnauthorizedAccessException(
                    "Only owner can clear chat for everyone.");

            room.ClearChatForAll();

                        room.ClearLastMessage();

            await _uow.CommitAsync(ct);

            var recipients = room.GetMemberIds().ToList();

            await _broadcaster.ChatClearedAsync(room.Id, recipients, forEveryone: true);

                        var emptyUpdateForAll = new RoomUpdatedDto
            {
                RoomId = room.Id.Value,
                MessageId = Guid.Empty,
                SenderId = Guid.Empty,
                Preview = string.Empty,
                CreatedAt = DateTime.UtcNow,
                UnreadDelta = 0,
                RoomName = room.Name,
                RoomType = room.Type.ToString(),
                IsClearEvent = true,
                IsMuted = false             };
            await _broadcaster.RoomUpdatedAsync(emptyUpdateForAll, recipients);
        }
        else
        {
            room.ClearChatForMember(command.RequesterId);

                        room.ClearLastMessage();

            await _uow.CommitAsync(ct);

            await _broadcaster.ChatClearedAsync(
                room.Id,
                new[] { command.RequesterId },
                forEveryone: false);

                        var emptyUpdateForOne = new RoomUpdatedDto
            {
                RoomId = room.Id.Value,
                MessageId = Guid.Empty,
                SenderId = Guid.Empty,
                Preview = string.Empty,
                CreatedAt = DateTime.UtcNow,
                UnreadDelta = 0,
                IsClearEvent = true,
                RoomName = room.Name,
                RoomType = room.Type.ToString()
                            };
            await _broadcaster.RoomUpdatedAsync(emptyUpdateForOne, new[] { command.RequesterId });
        }

        return Unit.Value;
    }
}