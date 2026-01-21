using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using System.Net;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class SendMessageCommandHandler
{
    private readonly IChatRoomRepository _roomRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserBlockRepository _blockRepository;
    private readonly IRoomAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;

    public SendMessageCommandHandler(
        IChatRoomRepository roomRepository,
        IMessageRepository messageRepository,
        IUserBlockRepository blockRepository,
        IRoomAuthorizationService authorization,
        IUnitOfWork unitOfWork)
    {
        _roomRepository = roomRepository;
        _messageRepository = messageRepository;
        _blockRepository = blockRepository;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
    }

    public async Task<MessageDto> Handle(
     SendMessageCommand command,
     CancellationToken cancellationToken)
    {
        await _authorization.EnsureUserIsMemberAsync(
            command.RoomId,
            command.SenderId,
            cancellationToken);

        var room = await _roomRepository.GetByIdAsync(
            command.RoomId,
            cancellationToken)
            ?? throw new InvalidOperationException("Chat room not found.");

        var recipients = room.Members
            .Select(m => m.UserId)
            .Where(id => id != command.SenderId)
            .ToList();

        if (room.Type == RoomType.Private && recipients.Count == 1)
        {
            var blocked = await _blockRepository.IsBlockedAsync(
                command.SenderId,
                recipients[0],
                cancellationToken);

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
            RoomId = command.RoomId.Value,
            SenderId = command.SenderId.Value,
            Content = message.Content,
            CreatedAt = message.CreatedAt
        };
    }

}
