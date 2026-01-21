using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record DeleteGroupCommand(
    RoomId RoomId,
    UserId RequesterId
) : IRequest<Unit>;
