using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class UnmuteRoomCommandHandler
    : IRequestHandler<UnmuteRoomCommand, Unit>
{
    private readonly IMutedRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster _broadcaster;     private readonly IChatRoomRepository _roomRepo;    
    public UnmuteRoomCommandHandler(
        IMutedRoomRepository repo,
        IUnitOfWork uow,
        IRoomAuthorizationService auth,
        IMessageBroadcaster broadcaster,            IChatRoomRepository roomRepo)           {
        _repo = repo;
        _uow = uow;
        _auth = auth;
        _broadcaster = broadcaster;                  _roomRepo = roomRepo;                     }

    public async Task<Unit> Handle(UnmuteRoomCommand command, CancellationToken ct)
    {
        try
        {
            await _auth.EnsureUserIsMemberAsync(command.RoomId, command.UserId, ct);
        }
        catch
        {
            return Unit.Value;
        }

        await _repo.RemoveAsync(command.RoomId, command.UserId, ct);
        await _uow.CommitAsync(ct);

                await BroadcastUnmuteUpdateAsync(command.RoomId, command.UserId, isMuted: false, ct);

        return Unit.Value;
    }

    private async Task BroadcastUnmuteUpdateAsync(RoomId roomId, UserId userId, bool isMuted, CancellationToken ct)
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
            Console.WriteLine($"[UnmuteRoom] Broadcasted unmute update for user {userId.Value} in room {roomId.Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UnmuteRoom] Error broadcasting unmute update: {ex.Message}");
        }
    }
}