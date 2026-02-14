using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class MarkRoomReadCommandHandler
    : IRequestHandler<MarkRoomReadCommand, Unit>
{
    private readonly IMessageRepository _messageRepo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster? _broadcaster;
    private readonly IChatRoomRepository _roomRepository;

    public MarkRoomReadCommandHandler(
        IMessageRepository messageRepo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IChatRoomRepository roomRepository,
        IMessageBroadcaster? broadcaster = null)
    {
        _messageRepo = messageRepo;
        _auth = auth;
        _uow = uow;
        _roomRepository = roomRepository;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(MarkRoomReadCommand command, CancellationToken ct)
    {
        Console.WriteLine($"[MarkRoomRead] Room={command.RoomId.Value}, User={command.UserId.Value}, LastMsg={command.LastMessageId.Value}");
        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.UserId, ct);

        var lastCreatedAt = await _messageRepo.GetCreatedAtAsync(command.LastMessageId, ct);
        if (lastCreatedAt is null)
        {
            Console.WriteLine("[MarkRoomRead] Last message not found");
            return Unit.Value;
        }

        // جيب الرسائل اللي كانت unread فعلاً
        var unreadBefore = await _messageRepo.GetUnreadUpToAsync(
            command.RoomId,
            lastCreatedAt.Value,
            command.UserId,
            take: 5000,
            ct: ct);

        // Bulk mark read
        await _messageRepo.BulkMarkReadUpToAsync(command.RoomId, lastCreatedAt.Value, command.UserId, ct);

        // تحديث LastRead
        await _roomRepository.UpdateMemberLastReadAsync(
            command.RoomId,
            command.UserId,
            command.LastMessageId,
            ct);

        await _uow.CommitAsync(ct);

        Console.WriteLine($"[MarkRoomRead] Marked read count={unreadBefore.Count}");

        if (_broadcaster is not null)
        {
            // جيب كل أعضاء الروم (بما فيهم الـ sender)
            var room = await _roomRepository.GetByIdWithMembersAsync(command.RoomId, ct);
            if (room != null)
            {
                var allMembers = room.Members.Select(m => m.UserId).ToList();

                // 1. ابعت MessageReadToAll لكل الرسائل اللي تأثرت
                // (ده اللي هيخلي الـ sender يحدث الـ blue ticks)
                foreach (var msg in unreadBefore)
                {
                    // ابعت لكل الأعضاء (بما فيهم الـ sender)
                    await _broadcaster.MessageReadToAllAsync(
                        msg.Id,
                        msg.SenderId,           // اللي أرسل الرسالة
                        allMembers);            // كل الأعضاء (بما فيهم الـ sender)
                }

                // 2. RoomUpdated (لتحديث الـ unread count في الـ sidebar)
                var update = new RoomUpdatedDto
                {
                    RoomId = command.RoomId.Value,
                    MessageId = command.LastMessageId.Value,
                    SenderId = command.UserId.Value,
                    Preview = "",
                    CreatedAt = DateTime.UtcNow,
                    UnreadDelta = -unreadBefore.Count, // ← الأفضل (مش -1)
                    RoomName = room.Name,
                    RoomType = room.Type.ToString()
                };
                await _broadcaster.RoomUpdatedAsync(update, allMembers);

                Console.WriteLine($"[MarkRoomRead] Broadcasted MessageReadToAll for {unreadBefore.Count} messages + RoomUpdated to {allMembers.Count} members");
            }
        }

        return Unit.Value;
    }
}
