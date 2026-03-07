
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, MessageDto>
{
    private readonly IChatRoomRepository _roomRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserBlockRepository _blockRepository;
    private readonly IRoomAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBroadcaster? _broadcaster;
    private readonly IUserDirectoryService _userDirectory;
    private readonly IPresenceService _presenceService;
    private readonly IMediator _mediator;
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IMutedRoomRepository _mutedRoomRepo;
    public SendMessageCommandHandler(
        IChatRoomRepository roomRepository,
        IMessageRepository messageRepository,
        IUserBlockRepository blockRepository,
        IRoomAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IUserDirectoryService userDirectory,
        IPresenceService presenceService,
        IMediator mediator,
        IMessageReceiptRepository receiptRepo,
        IMessageBroadcaster? broadcaster = null,
        IMutedRoomRepository mutedRoomRepo = null)
    {
        _roomRepository = roomRepository;
        _messageRepository = messageRepository;
        _blockRepository = blockRepository;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _userDirectory = userDirectory;
        _presenceService = presenceService;
        _mediator = mediator;
        _receiptRepo = receiptRepo;
        _broadcaster = broadcaster;
        _mutedRoomRepo = mutedRoomRepo;
    }

    public async Task<MessageDto> Handle(SendMessageCommand command, CancellationToken ct)
    {
        await _authorization.EnsureUserIsMemberAsync(command.RoomId, command.SenderId, ct);

        var room = await _roomRepository.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Chat room not found.");

        var recipients = room.Members
            .Select(m => m.UserId)
            .Where(id => id != command.SenderId)
            .ToList();

                bool isBlocked = false;
        if (room.Type == RoomType.Private && recipients.Count == 1)
        {
            var receiverId = recipients[0];
            isBlocked = await _blockRepository.IsBlockedAsync(receiverId, command.SenderId, ct);
        }

        if (isBlocked) recipients = new List<UserId>();

                ReplyInfoDto? replyInfo = null;
        if (command.ReplyToMessageId != null && command.ReplyToMessageId.Value != Guid.Empty)
        {
            var repliedMessage = await _messageRepository.GetByIdAsync(command.ReplyToMessageId, ct);
            if (repliedMessage is not null && repliedMessage.RoomId == command.RoomId)
            {
                var sender = await _userDirectory.GetUserAsync(repliedMessage.SenderId, ct);
                replyInfo = new ReplyInfoDto
                {
                    MessageId = repliedMessage.Id.Value,
                    SenderId = repliedMessage.SenderId.Value,
                    SenderName = sender?.DisplayName ?? "User",
                    ContentPreview = repliedMessage.Content.Length > 60 ? repliedMessage.Content[..60] + "…" : repliedMessage.Content,
                    CreatedAt = repliedMessage.CreatedAt,
                    IsDeleted = false
                };
            }
        }

                        var message = new Message(
            command.RoomId,
            command.SenderId,
            command.Content,
            recipients,
            command.ReplyToMessageId,
            isBlocked);

        await _messageRepository.AddAsync(message, ct);

        if (!isBlocked)
        {
                        room.RestoreMember(command.SenderId);
            foreach (var recipient in recipients)
            {
                room.RestoreMember(recipient);
            }

                        room.UpdateLastMessage(message);

                        _roomRepository.Update(room);
        }

                await _unitOfWork.CommitAsync(ct);

                var onlineUsers = await _presenceService.GetOnlineUsersAsync();
        var toDeliverImmediately = recipients
            .Where(r => onlineUsers.Any(o => o.Value == r.Value))
            .ToList();

        foreach (var userId in toDeliverImmediately)
        {
            await _mediator.Send(new DeliverMessageCommand(message.Id, userId), ct);
        }

                await _unitOfWork.CommitAsync(ct);

                var finalStats = await _receiptRepo.GetMessageStatsAsync(message.Id, ct);

                var dto = new MessageDto
        {
            Id = message.Id.Value,
            RoomId = command.RoomId.Value,
            SenderId = command.SenderId.Value,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            ReplyInfo = replyInfo,
            ReplyToMessageId = command.ReplyToMessageId?.Value,
            Status = finalStats.ReadCount >= finalStats.TotalRecipients ? MessageStatus.Read :
                     finalStats.DeliveredCount > 0 ? MessageStatus.Delivered : MessageStatus.Sent,
            ReadCount = finalStats.ReadCount,
            DeliveredCount = finalStats.DeliveredCount,
            TotalRecipients = finalStats.TotalRecipients,
            IsEdited = false,
            IsDeleted = false
        };

                        if (_broadcaster is not null)
        {
            try
            {
                var preview = dto.Content.Length > 80 ? dto.Content[..80] + "…" : dto.Content;

                                var allMembers = room.Members.Select(m => m.UserId).ToList();

                                var mutedUsers = new List<UserId>();
                var nonMutedUsers = new List<UserId>();

                foreach (var userId in allMembers)
                {
                                                            bool isMuted = await _mutedRoomRepo.IsMutedAsync(command.RoomId, userId, ct);
                    if (isMuted)
                        mutedUsers.Add(userId);
                    else
                        nonMutedUsers.Add(userId);
                }
                var senderUser = await _userDirectory.GetUserAsync(command.SenderId, ct);
                var senderName = senderUser?.DisplayName ?? "";

                                if (nonMutedUsers.Any())
                {
                    var recipientUpdateDto = new RoomUpdatedDto
                    {
                        RoomId = dto.RoomId,
                        MessageId = dto.Id,
                        SenderId = dto.SenderId,
                        Preview = preview,
                        CreatedAt = dto.CreatedAt,
                        UnreadDelta = 1,
                        RoomName = room.Name,
                        RoomType = room.Type.ToString(),
                        SenderName = senderName,
                        IsMuted = false                     };
                    await _broadcaster.RoomUpdatedAsync(recipientUpdateDto, nonMutedUsers);
                }

                                var zeroDeltaUsers = mutedUsers.Concat(new[] { command.SenderId }).Distinct().ToList();
                if (zeroDeltaUsers.Any())
                {
                    var zeroDeltaDto = new RoomUpdatedDto
                    {
                        RoomId = dto.RoomId,
                        MessageId = dto.Id,
                        SenderId = dto.SenderId,
                        Preview = preview,
                        CreatedAt = dto.CreatedAt,
                        SenderName = senderName,
                        UnreadDelta = 0,
                        RoomName = room.Name,
                        RoomType = room.Type.ToString(),
                        IsMuted = true                     };
                    await _broadcaster.RoomUpdatedAsync(zeroDeltaDto, zeroDeltaUsers);
                }

                                await _broadcaster.BroadcastMessageAsync(dto, allMembers);
                await _broadcaster.MessageReceiptStatsUpdatedAsync(
                    dto.Id,
                    command.RoomId.Value,
                    finalStats.TotalRecipients,
                    finalStats.DeliveredCount,
                    finalStats.ReadCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broadcasting Error] {ex.Message}");
            }
        }

        return dto;
    }
}