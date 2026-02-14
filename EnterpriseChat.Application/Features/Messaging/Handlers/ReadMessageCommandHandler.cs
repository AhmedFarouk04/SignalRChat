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
        var stats = msg.GetReceiptStats();

        // ✅ البث لكل الأعضاء (تصحيح الخطأ)
        var tasks = new List<Task>();

        foreach (var memberId in roomMembers)
        {
            tasks.Add(_broadcaster.MessageStatusUpdatedAsync(
                command.MessageId,
                command.UserId,
                MessageStatus.Read,
                new List<UserId> { memberId }));  // ✅ حولها لـ List مش array

            // لو العضو ده هو الـ sender، ابعتله إحصائية كاملة
            if (memberId == msg.SenderId)
            {
                tasks.Add(_broadcaster.MessageReceiptStatsUpdatedAsync(
                    command.MessageId.Value,
                    memberId.Value,
                    stats.TotalRecipients,
                    stats.DeliveredCount,
                    stats.ReadCount));
            }
        }

        await Task.WhenAll(tasks);
        return Unit.Value;
    }
}