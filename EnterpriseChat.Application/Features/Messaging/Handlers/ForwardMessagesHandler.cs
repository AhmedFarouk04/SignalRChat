using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
// EnterpriseChat.Application/Features/Messaging/Handlers/ForwardMessagesHandler.cs
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;


namespace EnterpriseChat.Application.Features.Messaging.Handlers
{
    
    public sealed class ForwardMessagesHandler : IRequestHandler<ForwardMessagesCommand, bool>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IChatRoomRepository _roomRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMessageBroadcaster _broadcaster;

        public ForwardMessagesHandler(
            IMessageRepository messageRepository,
            IChatRoomRepository roomRepository,
            IUnitOfWork unitOfWork,
            IMessageBroadcaster broadcaster)
        {
            _messageRepository = messageRepository;
            _roomRepository = roomRepository;
            _unitOfWork = unitOfWork;
            _broadcaster = broadcaster;
        }

        public async Task<bool> Handle(ForwardMessagesCommand command, CancellationToken ct)
        {
            // 1. جلب الرسائل الأصلية المطلوب عمل Forward لها
            var originalMessages = new List<Message>();
            foreach (var mId in command.MessageIds)
            {
                var msg = await _messageRepository.GetByIdAsync(new MessageId(mId), ct);
                if (msg != null && !msg.IsDeleted) originalMessages.Add(msg);
            }

            if (!originalMessages.Any()) return false;

            var senderIdVo = new UserId(command.SenderId);

            // 2. تكرار الرسائل لكل غرفة مستهدفة
            foreach (var targetRoomId in command.TargetRoomIds)
            {
                var roomIdVo = new RoomId(targetRoomId);
                var room = await _roomRepository.GetByIdWithMembersAsync(roomIdVo, ct);

                if (room == null || !room.IsMember(senderIdVo)) continue;

                var recipients = room.Members
                    .Select(m => m.UserId)
                    .Where(id => id != senderIdVo)
                    .ToList();

                foreach (var originalMsg in originalMessages)
                {
                    // إنشاء رسالة جديدة محتواها هو نفس محتوى الرسالة القديمة
                    // ملاحظة: يمكنك إضافة "Forwarded" badge في الـ UI لاحقاً
                    var newMessage = new Message(
                        roomIdVo,
                        senderIdVo,
                        originalMsg.Content,
                        recipients,
                        null // الـ Forward لا يعتبر Reply
                    );

                    await _messageRepository.AddAsync(newMessage, ct);

                    // 3. التبليغ Real-time لكل غرفة
                    // هنا بنبعت DTO بسيط عشان الناس تحس بالرسالة فوراً
                    var dto = MappingToDto(newMessage, recipients.Count);
                    await _broadcaster.BroadcastMessageAsync(dto, recipients);
                }
            }

            await _unitOfWork.CommitAsync(ct);
            return true;
        }

        private MessageDto MappingToDto(Message msg, int recipientsCount) => new MessageDto
        {
            Id = msg.Id.Value,
            RoomId = msg.RoomId.Value,
            SenderId = msg.SenderId.Value,
            Content = msg.Content,
            CreatedAt = msg.CreatedAt,
            Status = MessageStatus.Sent,
            TotalRecipients = recipientsCount
        };
    }
}
