using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.Features.Messaging.Handlers
{
    // EnterpriseChat.Application/Features/Messaging/Handlers/DeleteMessageCommandHandler.cs
    public sealed class DeleteMessageCommandHandler : IRequestHandler<DeleteMessageCommand, Unit>
    {
        private readonly IMessageRepository _messageRepo;
        private readonly IChatRoomRepository _roomRepo;
        private readonly IUnitOfWork _uow;
        private readonly IMessageBroadcaster _broadcaster;

        public DeleteMessageCommandHandler(IMessageRepository messageRepo, IChatRoomRepository roomRepo, IUnitOfWork uow, IMessageBroadcaster broadcaster)
        {
            _messageRepo = messageRepo;
            _roomRepo = roomRepo;
            _uow = uow;
            _broadcaster = broadcaster;
        }

        public async Task<Unit> Handle(DeleteMessageCommand request, CancellationToken ct)
        {
            var message = await _messageRepo.GetByIdAsync(request.MessageId, ct)
                          ?? throw new InvalidOperationException("Message not found.");

            var members = await _messageRepo.GetRoomMemberIdsAsync(message.RoomId, ct);

            if (request.DeleteForEveryone)
            {
                var room = await _roomRepo.GetByIdAsync(message.RoomId, ct);
                bool isSender = message.SenderId == request.UserId;
                // التحقق لو المستخدم أدمن أو مالك الغرفة
                bool isAdminOrOwner = room.OwnerId == request.UserId ||
                                      room.Members.Any(m => m.UserId == request.UserId && m.IsAdmin);

                if (!isSender && !isAdminOrOwner)
                    throw new UnauthorizedAccessException("Unauthorized to delete for everyone.");

                message.Delete(); // تحديث حالة الـ Entity
                await _uow.CommitAsync(ct);

                await _broadcaster.MessageDeletedAsync(message.Id, members);
                await _broadcaster.RoomUpdatedAsync(new RoomUpdatedDto
                {
                    RoomId = message.RoomId.Value,
                    MessageId = message.Id.Value,
                    Preview = "🚫 This message was deleted",
                    CreatedAt = DateTime.UtcNow
                }, members);
            }
            else
            {
                // حذف لي فقط: نرسل حدث للمستخدم الحالي فقط ليخفيها من الـ UI
                await _broadcaster.MessageDeletedAsync(message.Id, new[] { request.UserId });
            }

            return Unit.Value;
        }
    }
}
