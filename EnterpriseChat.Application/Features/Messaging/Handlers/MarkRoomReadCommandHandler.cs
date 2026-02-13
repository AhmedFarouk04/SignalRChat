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

        // ✅ (A) هات الرسائل اللي كانت Unread فعلاً عند القارئ (قبل التحديث)
        // دي اللي لها Receipt للقارئ وكانت < Read
        var unreadBefore = await _messageRepo.GetUnreadUpToAsync(
            command.RoomId,
            lastCreatedAt.Value,
            command.UserId,
            take: 5000,
            ct: ct);

        // ✅ 1) Bulk mark read في الـ DB
        await _messageRepo.BulkMarkReadUpToAsync(command.RoomId, lastCreatedAt.Value, command.UserId, ct);

        // ✅ 2) تحديث LastReadMessageId
        await _roomRepository.UpdateMemberLastReadAsync(
            command.RoomId,
            command.UserId,
            command.LastMessageId,
            ct);

        await _uow.CommitAsync(ct);

        Console.WriteLine($"[MarkRoomRead] Marked read count={unreadBefore.Count}");

        // ✅ 3) ابعت MessageRead فقط للـ senders بتوع الرسائل اللي كانت Unread فعلاً
        // (مش كل رسائل الروم)
        if (_broadcaster is not null && unreadBefore.Count > 0)
        {
            var tasks = unreadBefore
                .Where(x => x.SenderId != command.UserId) // احتياط
                .Select(x => _broadcaster.MessageReadAsync(x.Id, x.SenderId))
                .ToList();

            await Task.WhenAll(tasks);
        }

        // ✅ 4) RoomUpdated (اختياري)
        // ملحوظة: delta=-1 دي غالباً غلط في حالة MarkRoomRead لأنها ممكن تكون قرأت أكتر من رسالة.
        // لو عايزها صح: استخدم -unreadBefore.Count
        if (_broadcaster is not null)
        {
            var room = await _roomRepository.GetByIdWithMembersAsync(command.RoomId, ct);
            if (room != null)
            {
                var members = room.Members.Select(m => m.UserId).ToList();

                var update = new RoomUpdatedDto
                {
                    RoomId = command.RoomId.Value,
                    MessageId = command.LastMessageId.Value,
                    SenderId = command.UserId.Value,
                    Preview = "",
                    CreatedAt = DateTime.UtcNow,
                    UnreadDelta = -Math.Max(1, unreadBefore.Count), // ✅ الأفضل -unreadBefore.Count
                    RoomName = room.Name,
                    RoomType = room.Type.ToString()
                };

                await _broadcaster.RoomUpdatedAsync(update, members);
                Console.WriteLine($"[MarkRoomRead] RoomUpdated delta={update.UnreadDelta} sent to {members.Count}");
            }
        }

        return Unit.Value;
    }
}
