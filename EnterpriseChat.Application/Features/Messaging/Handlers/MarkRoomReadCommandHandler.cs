using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class MarkRoomReadCommandHandler : IRequestHandler<MarkRoomReadCommand, Unit>
{
    private readonly IMessageRepository _messageRepo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster? _broadcaster;
    private readonly IChatRoomRepository _roomRepository;
    private readonly IMessageReceiptRepository _receiptRepo;

    public MarkRoomReadCommandHandler(
        IMessageRepository messageRepo,
        IMessageReceiptRepository receiptRepo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IChatRoomRepository roomRepository,
        IMessageBroadcaster? broadcaster = null)
    {
        _messageRepo = messageRepo;
        _receiptRepo = receiptRepo;
        _auth = auth;
        _uow = uow;
        _roomRepository = roomRepository;
        _broadcaster = broadcaster;
    }

    // داخل MarkRoomReadCommandHandler.cs
    public async Task<Unit> Handle(MarkRoomReadCommand command, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.UserId, ct);
        var lastCreatedAt = await _messageRepo.GetCreatedAtAsync(command.LastMessageId, ct);
        if (lastCreatedAt is null) return Unit.Value;

        var unreadBefore = await _messageRepo.GetUnreadUpToAsync(command.RoomId, lastCreatedAt.Value, command.UserId, 1000, ct);
        if (!unreadBefore.Any()) return Unit.Value;

        await _messageRepo.BulkMarkReadUpToAsync(command.RoomId, lastCreatedAt.Value, command.UserId, ct);
        await _roomRepository.UpdateMemberLastReadAsync(command.RoomId, command.UserId, command.LastMessageId, ct);
        await _uow.CommitAsync(ct);

        if (_broadcaster is not null)
        {
            var room = await _roomRepository.GetByIdWithMembersAsync(command.RoomId, ct);
            var allMembers = room?.Members.Select(m => m.UserId).ToList() ?? new();

            foreach (var msg in unreadBefore)
            {
                // 1. إبلاغ الجميع أن العضو "فلان" قرأ الرسالة
                await _broadcaster.MessageStatusUpdatedAsync(msg.Id, command.UserId, MessageStatus.Read, allMembers);

                // 2. 🚀 الخطوة الأهم: إرسال الإحصائيات المحدثة للمرسل فوراً لضمان عدم تلوينها بالأزرق بالخطأ
                var stats = await _receiptRepo.GetMessageStatsAsync(msg.Id, ct);
                await _broadcaster.MessageReceiptStatsUpdatedAsync(
                    msg.Id.Value,
                    command.RoomId.Value,
                    stats.TotalRecipients,
                    stats.DeliveredCount,
                    stats.ReadCount);
            }

            // تحديث العداد للمستخدم الذي قرأ
            await _broadcaster.RoomUpdatedAsync(new RoomUpdatedDto
            {
                RoomId = command.RoomId.Value,
                UnreadDelta = -unreadBefore.Count
            }, new[] { command.UserId });
        }
        return Unit.Value;
    }
}