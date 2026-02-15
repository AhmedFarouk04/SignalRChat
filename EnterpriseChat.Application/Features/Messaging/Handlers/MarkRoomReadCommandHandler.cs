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
    private readonly IMessageReceiptRepository _receiptRepo;

    public MarkRoomReadCommandHandler(
    IMessageRepository messageRepo,
    IMessageReceiptRepository receiptRepo,     // ✅ ADD
    IRoomAuthorizationService auth,
    IUnitOfWork uow,
    IChatRoomRepository roomRepository,
    IMessageBroadcaster? broadcaster = null)
    {
        _messageRepo = messageRepo;
        _receiptRepo = receiptRepo;                // ✅ ADD
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
            var room = await _roomRepository.GetByIdWithMembersAsync(command.RoomId, ct);
            if (room is null) return Unit.Value;

            var allMembers = room.Members.Select(m => m.UserId).ToList();

            var tasks = new List<Task>();

            foreach (var msg in unreadBefore)
            {
                // ✅ Read status للكل (مرّة واحدة لكل رسالة)
                tasks.Add(_broadcaster.MessageStatusUpdatedAsync(
                    msg.Id,
                    command.UserId,                 // اللي قرأ فعلاً
                    MessageStatus.Read,
                    allMembers));

                // ✅ Stats للـ sender عشان ✓✓ تبقى زرقا عنده
                tasks.Add(Task.Run(async () =>
                {
                    var stats = await _receiptRepo.GetMessageStatsAsync(msg.Id, ct);

                    await _broadcaster.MessageReceiptStatsUpdatedAsync(
                        msg.Id.Value,
                        msg.SenderId.Value,
                        stats.TotalRecipients,
                        stats.DeliveredCount,
                        stats.ReadCount);
                }, ct));
            }

            // ✅ تحديث sidebar unread count
            var update = new RoomUpdatedDto
            {
                RoomId = command.RoomId.Value,
                MessageId = command.LastMessageId.Value,
                SenderId = command.UserId.Value,
                Preview = "",
                CreatedAt = DateTime.UtcNow,
                UnreadDelta = -unreadBefore.Count,
                RoomName = room.Name,
                RoomType = room.Type.ToString()
            };

            tasks.Add(_broadcaster.RoomUpdatedAsync(update, allMembers));

            await Task.WhenAll(tasks);
        }


        return Unit.Value;
    }
}
