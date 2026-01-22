using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record TransferGroupOwnershipCommand(
    RoomId RoomId,
    UserId RequesterId,
    UserId NewOwnerId
) : IRequest<Unit>;
