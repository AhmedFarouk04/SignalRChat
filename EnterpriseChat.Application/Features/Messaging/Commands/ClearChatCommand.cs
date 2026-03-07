using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public record ClearChatCommand(
    RoomId RoomId,
    UserId RequesterId,
    bool ForEveryone
) : IRequest<Unit>;