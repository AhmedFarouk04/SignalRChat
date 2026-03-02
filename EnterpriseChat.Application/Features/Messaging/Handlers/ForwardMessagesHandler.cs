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
            var originalMessages = new List<Message>();
            foreach (var mId in command.MessageIds)
            {
                var msg = await _messageRepository.GetByIdAsync(new MessageId(mId), ct);
                if (msg != null && !msg.IsDeleted) originalMessages.Add(msg);
            }

            if (!originalMessages.Any()) return false;

            var senderIdVo = new UserId(command.SenderId);

            foreach (var targetRoomId in command.TargetRoomIds)
            {
                var roomIdVo = new RoomId(targetRoomId);
                var room = await _roomRepository.GetByIdWithMembersAsync(roomIdVo, ct);
                if (room == null || !room.IsMember(senderIdVo)) continue;

                var recipients = room.Members
                    .Select(m => m.UserId)
                    .Where(id => id != senderIdVo)
                    .ToList();

                var allMembers = recipients.Concat(new[] { senderIdVo }).ToList();
                var messagesToBroadcast = new List<(MessageDto dto, RoomUpdatedDto roomUpdate)>();

                foreach (var originalMsg in originalMessages)
                {
                    var newMessage = new Message(roomIdVo, senderIdVo, originalMsg.Content, recipients, null);
                    await _messageRepository.AddAsync(newMessage, ct);

                    messagesToBroadcast.Add((
                        MappingToDto(newMessage, recipients.Count),
                        new RoomUpdatedDto
                        {
                            RoomId = targetRoomId,
                            MessageId = newMessage.Id.Value,
                            SenderId = command.SenderId,
                            Preview = newMessage.Content.Length > 60
                                ? newMessage.Content[..60] + "…"
                                : newMessage.Content,
                            CreatedAt = newMessage.CreatedAt,
                            UnreadDelta = 1
                        }
                    ));
                }

                // ✅ Commit الأول قبل أي broadcast
                await _unitOfWork.CommitAsync(ct);

                // ✅ بعد الـ commit، ابعت SignalR
                foreach (var (dto, roomUpdate) in messagesToBroadcast)
                {
                    await _broadcaster.BroadcastToRoomGroupAsync(targetRoomId, dto);

                    await _broadcaster.RoomUpdatedAsync(roomUpdate, allMembers);
                }
            }

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
