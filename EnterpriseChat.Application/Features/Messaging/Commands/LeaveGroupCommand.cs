using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record LeaveGroupCommand(
    RoomId RoomId,
    UserId RequesterId
) : IRequest<Unit>;
