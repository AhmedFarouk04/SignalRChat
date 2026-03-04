using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;


namespace EnterpriseChat.Application.Features.Messaging.Handlers
{


    // EnterpriseChat.Application/Features/Messaging/Handlers/EditMessageCommandHandler.cs
    public sealed class EditMessageCommandHandler : IRequestHandler<EditMessageCommand, Unit>
    {
        private readonly IMessageRepository _messageRepo;
        private readonly IUnitOfWork _uow;
        private readonly IMessageBroadcaster _broadcaster;

        public EditMessageCommandHandler(
            IMessageRepository messageRepo,
            IUnitOfWork uow,
            IMessageBroadcaster broadcaster)
        {
            _messageRepo = messageRepo;
            _uow = uow;
            _broadcaster = broadcaster;
        }

        public async Task<Unit> Handle(EditMessageCommand request, CancellationToken ct)
        {
            var message = await _messageRepo.GetByIdWithReceiptsAsync(request.MessageId, ct)
                ?? throw new InvalidOperationException("Message not found.");

            if (message.SenderId != request.UserId)
                throw new UnauthorizedAccessException("You can only edit your own messages.");
            if (!message.CanEdit(DateTime.UtcNow))
                throw new InvalidOperationException("Message can no longer be edited.");
            if (message.IsDeleted)
                throw new InvalidOperationException("Cannot edit a deleted message.");

            message.Edit(request.NewContent);
            message.ClearReadReceipts(); // ✅ امسح Read بس
                                         // ✅ رجّع لـ Delivered - الناس لازم تقرأ التعديل

            await _uow.CommitAsync(ct);

            var members = await _messageRepo.GetRoomMemberIdsAsync(message.RoomId, ct);
            var stats = message.GetReceiptStats(); // ← دلوقتي read=0, delivered=N

            await _broadcaster.MessageReceiptStatsUpdatedAsync(
                message.Id.Value, message.RoomId.Value,
                stats.TotalRecipients, stats.DeliveredCount, stats.ReadCount);

            await _broadcaster.MessageUpdatedAsync(message.Id.Value, message.Content, members);

            return Unit.Value;
        }
    }
}
