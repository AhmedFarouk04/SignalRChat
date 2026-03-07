using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class MuteRoomCommandHandler
    : IRequestHandler<MuteRoomCommand, Unit>
{
    private readonly IMutedRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster _broadcaster;     private readonly IChatRoomRepository _roomRepo;    
    public MuteRoomCommandHandler(
        IMutedRoomRepository repo,
        IUnitOfWork uow,
        IRoomAuthorizationService auth,
        IMessageBroadcaster broadcaster,            IChatRoomRepository roomRepo)           {
        _repo = repo;
        _uow = uow;
        _auth = auth;
        _broadcaster = broadcaster;                  _roomRepo = roomRepo;                     }

    public async Task<Unit> Handle(MuteRoomCommand command, CancellationToken ct)
    {
        Console.WriteLine($"[MuteRoom] 🟢 HANDLER STARTED for user {command.UserId.Value} in room {command.RoomId.Value}");

        try
        {
            await _auth.EnsureUserIsMemberAsync(command.RoomId, command.UserId, ct);
        }
        catch
        {
            return Unit.Value;
        }

        if (await _repo.IsMutedAsync(command.RoomId, command.UserId, ct))
            return Unit.Value;

        await _repo.AddAsync(MutedRoom.Create(command.RoomId, command.UserId), ct);
        await _uow.CommitAsync(ct);
        Console.WriteLine($"[MuteRoom] ✅ Commit successful, about to broadcast...");

                await BroadcastMuteUpdateAsync(command.RoomId, command.UserId, isMuted: true, ct);

        return Unit.Value;
    }

    private async Task BroadcastMuteUpdateAsync(RoomId roomId, UserId userId, bool isMuted, CancellationToken ct)
    {
        try
        {
            var room = await _roomRepo.GetByIdAsync(roomId, ct);
            if (room == null) return;

                        var updateDto = new RoomUpdatedDto
            {
                RoomId = roomId.Value,
                MessageId = Guid.Empty,
                SenderId = Guid.Empty,
                Preview = string.Empty,
                CreatedAt = DateTime.UtcNow,
                UnreadDelta = 0,
                RoomName = room.Name,
                RoomType = room.Type.ToString(),
                IsMuted = isMuted,                           IsClearEvent = false
            };

            await _broadcaster.RoomUpdatedAsync(updateDto, new[] { userId });
            Console.WriteLine($"[MuteRoom] Broadcasted mute update for user {userId.Value} in room {roomId.Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MuteRoom] Error broadcasting mute update: {ex.Message}");
        }
    }
}