using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class DeliverMessageCommandHandler : IRequestHandler<DeliverMessageCommand, Unit>
{
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IMessageBroadcaster _broadcaster;

    public DeliverMessageCommandHandler(
        IMessageReceiptRepository receiptRepo,
        IMessageRepository messageRepo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster)
    {
        _receiptRepo = receiptRepo;
        _messageRepo = messageRepo;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(DeliverMessageCommand command, CancellationToken ct)
    {
        // 1. جلب الرسالة
        var msg = await _messageRepo.GetByIdAsync(command.MessageId.Value, ct);
        if (msg is null) return Unit.Value;

        // 2. جلب سجل الاستلام
        var receipt = await _receiptRepo.GetAsync(command.MessageId, command.UserId, ct);

        if (receipt is null)
        {
            await Task.Delay(100, ct);
            receipt = await _receiptRepo.GetAsync(command.MessageId, command.UserId, ct);
            if (receipt is null) return Unit.Value;
        }

        // 3. التحقق من الحالة الحالية
        if (receipt.Status >= MessageStatus.Delivered) return Unit.Value;

        // 4. تحديث الحالة وحفظها
        receipt.MarkDelivered();
        await _uow.CommitAsync(ct);

        // 5. جلب الإحصائيات المحدثة
        var stats = await _receiptRepo.GetMessageStatsAsync(command.MessageId, ct);
        Console.WriteLine($"[DeliverMessage] ✅ Delivered: msg={command.MessageId.Value} user={command.UserId.Value} d={stats.DeliveredCount} r={stats.ReadCount}");

        // 6. جلب أعضاء الغرفة (مهم جداً)
        var roomMembers = await _messageRepo.GetRoomMemberIdsAsync(msg.RoomId, ct);
        var allMembers = roomMembers.ToList();

        // ✅ التأكد من إضافة الـ Sender نفسه للقائمة
        if (!allMembers.Contains(msg.SenderId))
        {
            allMembers.Add(msg.SenderId);
        }

        // 7. 🔥 الأهم: بث التحديث لكل أعضاء الغرفة (خصوصاً الـ sender)
        // استخدم MessageStatusUpdated لكل عضو على حدة
        foreach (var memberId in allMembers)
        {
            await _broadcaster.MessageStatusUpdatedAsync(
                command.MessageId,
                command.UserId,
                MessageStatus.Delivered,
                new List<UserId> { memberId }); // نبعت لكل member لوحده
        }

        // 8. تحديث عدادات الرسالة للكل
        await _broadcaster.MessageReceiptStatsUpdatedAsync(
            command.MessageId.Value,
            msg.RoomId.Value,
            stats.TotalRecipients,
            stats.DeliveredCount,
            stats.ReadCount);

        return Unit.Value;
    }
}