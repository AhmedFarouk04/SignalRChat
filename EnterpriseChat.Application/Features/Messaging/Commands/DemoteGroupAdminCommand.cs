using EnterpriseChat.Domain.ValueObjects;
using MediatR;

public sealed record DemoteGroupAdminCommand(
    RoomId RoomId,
    UserId TargetUserId,
    UserId RequesterId
) : IRequest<Unit>;
