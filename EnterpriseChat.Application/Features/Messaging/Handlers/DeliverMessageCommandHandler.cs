using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

public sealed class DeliverMessageCommandHandler : IRequestHandler<DeliverMessageCommand, Unit>
{
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IMessageRepository _messageRepo;  // ✅ صحح الاسم
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IChatRoomRepository _roomRepo;

    public DeliverMessageCommandHandler(
        IMessageReceiptRepository receiptRepo,
        IMessageRepository messageRepo,  // ✅ تأكد من الاسم
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

    public async Task<Unit> Handle(DeliverMessageCommand command, CancellationToken ct)
    {
        // ✅ استخدم _messageRepo مش _messages
        var msg = await _messageRepo.GetByIdWithReceiptsAsync(command.MessageId, ct);
        if (msg is null) return Unit.Value;

        await _auth.EnsureUserIsMemberAsync(msg.RoomId, command.UserId, ct);

        // لو أنا الـ sender، متعملش Deliver
        if (msg.SenderId == command.UserId) return Unit.Value;

        var receipt = await _receiptRepo.GetAsync(command.MessageId, command.UserId, ct);
        if (receipt is null) return Unit.Value;

        if (receipt.Status >= MessageStatus.Delivered) return Unit.Value;

        receipt.MarkDelivered();
        await _uow.CommitAsync(ct);

        var roomMembers = await _messageRepo.GetRoomMemberIdsAsync(msg.RoomId, ct);
        var stats = await _receiptRepo.GetMessageStatsAsync(command.MessageId, ct);

        var tasks = new List<Task>();

        foreach (var memberId in roomMembers)
        {
            tasks.Add(_broadcaster.MessageStatusUpdatedAsync(
                command.MessageId,
                command.UserId,
                MessageStatus.Delivered,
                new List<UserId> { memberId }));  // ✅ حولها لـ List

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