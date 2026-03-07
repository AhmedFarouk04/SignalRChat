using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public record DeleteChatCommand(
    RoomId RoomId,
    UserId RequesterId
) : IRequest<Unit>;