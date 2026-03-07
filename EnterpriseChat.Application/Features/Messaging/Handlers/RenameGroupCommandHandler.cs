using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class RenameGroupCommandHandler : IRequestHandler<RenameGroupCommand, Unit>
{
    private readonly IChatRoomRepository _rooms;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;

    public RenameGroupCommandHandler(
        IChatRoomRepository rooms,
        IRoomAuthorizationService auth,
        IUnitOfWork uow)
    {
        _rooms = rooms;
        _auth = auth;
        _uow = uow;
    }

                public async Task<Unit> Handle(RenameGroupCommand request, CancellationToken ct)
    {
        Console.WriteLine($"[HANDLER] ========== RenameGroup START ==========");

        try
        {
                        await _auth.EnsureUserIsAdminAsync(request.RoomId, request.RequesterId, ct);

                        var room = await _rooms.GetByIdForUpdateAsync(request.RoomId, ct);
            if (room == null)
                throw new KeyNotFoundException("Room not found.");

            Console.WriteLine($"[HANDLER] Before rename: '{room.Name}'");

            room.Rename(request.Name);

            Console.WriteLine($"[HANDLER] After rename: '{room.Name}'");

            await _uow.CommitAsync(ct);

                        var verifyRoom = await _rooms.GetByIdAsync(request.RoomId, ct);
            Console.WriteLine($"[HANDLER] Verification after commit: '{verifyRoom?.Name}'");

            return Unit.Value;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HANDLER] ❌ Error: {ex}");
            throw;
        }
    }
}