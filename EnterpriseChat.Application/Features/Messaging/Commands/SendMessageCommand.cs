using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record SendMessageCommand(
    RoomId RoomId,
    UserId SenderId,
    string Content
) : IRequest<MessageDto>;
