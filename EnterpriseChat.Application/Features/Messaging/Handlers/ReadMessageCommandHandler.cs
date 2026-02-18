using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

public sealed class ReadMessageCommandHandler : IRequestHandler<ReadMessageCommand, Unit>
{
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IUnitOfWork _uow;
    private readonly IMessageRepository _messageRepo;  // ✅ صحح الاسم
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IChatRoomRepository _roomRepo;

    public ReadMessageCommandHandler(
        IMessageReceiptRepository receiptRepo,
        IMessageRepository messageRepo,  // ✅ تأكد إن اسم الباراميتر ده
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster,
        IChatRoomRepository roomRepo)
    {
        _receiptRepo = receiptRepo;
        _messageRepo = messageRepo;  // ✅ صحح التخصيص
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
        _roomRepo = roomRepo;
    }

    public async Task<Unit> Handle(ReadMessageCommand command, CancellationToken ct)
    {
        // ✅ استخدم _messageRepo مش _messages
        var msg = await _messageRepo.GetByIdWithReceiptsAsync(command.MessageId, ct);
        if (msg is null) return Unit.Value;

        await _auth.EnsureUserIsMemberAsync(msg.RoomId, command.UserId, ct);

        // لو أنا الـ sender، متعملش Read
        if (msg.SenderId == command.UserId) return Unit.Value;

        var receipt = await _receiptRepo.GetAsync(command.MessageId, command.UserId, ct);
        if (receipt is null) return Unit.Value;

        // لو مقروءة قبل كده، اسكت
        if (receipt.Status == MessageStatus.Read) return Unit.Value;

        receipt.MarkRead();
        await _uow.CommitAsync(ct);

        // ✅ جلب كل أعضاء الغرفة
        var roomMembers = await _messageRepo.GetRoomMemberIdsAsync(msg.RoomId, ct);
        var stats = await _receiptRepo.GetMessageStatsAsync(command.MessageId, ct);

        // 1) ابعت تحديث "Read" لكل أعضاء الروم مرة واحدة
        await _broadcaster.MessageStatusUpdatedAsync(
            command.MessageId,
            command.UserId,               // اللي قرأ
            MessageStatus.Read,
            roomMembers.ToList());

        // 2) ابعت receipt stats للـ sender فقط (هو اللي بيشوف ✓✓)
        await _broadcaster.MessageReceiptStatsUpdatedAsync(
            command.MessageId.Value,
             msg.RoomId.Value,           // ✅ sender
            stats.TotalRecipients,
            stats.DeliveredCount,
            stats.ReadCount);

        return Unit.Value;

    }
}