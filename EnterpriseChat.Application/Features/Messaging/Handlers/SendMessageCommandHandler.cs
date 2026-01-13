using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class SendMessageCommandHandler
{
    private readonly IChatRoomRepository _roomRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserBlockRepository _blockRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SendMessageCommandHandler(
        IChatRoomRepository roomRepository,
        IMessageRepository messageRepository,
        IUserBlockRepository blockRepository,
        IUnitOfWork unitOfWork)
    {
        _roomRepository = roomRepository;
        _messageRepository = messageRepository;
        _blockRepository = blockRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MessageDto> Handle(
        SendMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        var room = await _roomRepository.GetByIdAsync(command.RoomId, cancellationToken)
            ?? throw new InvalidOperationException("Chat room not found.");

        // 🔐 Member only
        if (!room.IsMember(command.SenderId))
            throw new UnauthorizedAccessException("User is not a member of this room.");

        var recipients = room.Members
            .Select(m => m.UserId)
            .Where(id => id != command.SenderId)
            .ToList();

        // 🔐 Block check (strict for private rooms)
        if (room.Type == Domain.Enums.RoomType.Private && recipients.Count == 1)
        {
            var blocked = await _blockRepository.IsBlockedAsync(
                command.SenderId,
                recipients[0]);

            if (blocked)
                throw new InvalidOperationException("User is blocked.");
        }

        var message = new Message(
            command.RoomId,
            command.SenderId,
            command.Content,
            recipients);

        await _messageRepository.AddAsync(message, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return new MessageDto
        {
            Id = message.Id.Value,
            RoomId = command.RoomId,
            SenderId = command.SenderId,
            Content = message.Content,
            CreatedAt = message.CreatedAt
        };
    }
}
