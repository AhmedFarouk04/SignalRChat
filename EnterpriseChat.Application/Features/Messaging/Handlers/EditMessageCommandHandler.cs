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

        public EditMessageCommandHandler(IMessageRepository messageRepo, IUnitOfWork uow, IMessageBroadcaster broadcaster)
        {
            _messageRepo = messageRepo;
            _uow = uow;
            _broadcaster = broadcaster;
        }

        public async Task<Unit> Handle(EditMessageCommand request, CancellationToken ct)
        {
            var message = await _messageRepo.GetByIdAsync(request.MessageId, ct)
                          ?? throw new InvalidOperationException("Message not found.");

            // التحقق من الملكية: صاحب الرسالة فقط
            if (message.SenderId != request.UserId)
                throw new UnauthorizedAccessException("You can only edit your own messages.");

            message.Edit(request.NewContent);
            await _uow.CommitAsync(ct);

            var members = await _messageRepo.GetRoomMemberIdsAsync(message.RoomId, ct);

            // تحديث Real-time للرسالة والـ Sidebar
            await _broadcaster.MessageUpdatedAsync(message.Id, message.Content, members);
            await _broadcaster.RoomUpdatedAsync(new RoomUpdatedDto
            {
                RoomId = message.RoomId.Value,
                MessageId = message.Id.Value,
                Preview = message.Content,
                CreatedAt = DateTime.UtcNow
            }, members);

            return Unit.Value;
        }
    }
}
