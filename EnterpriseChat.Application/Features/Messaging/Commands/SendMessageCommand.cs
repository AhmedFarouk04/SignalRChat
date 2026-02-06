using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

// في SendMessageCommand.cs أضف:
public sealed record SendMessageCommand(
    RoomId RoomId,
    UserId SenderId,
    string Content,
    MessageId? ReplyToMessageId = null  // ⬅️ جديد
) : IRequest<MessageDto>;