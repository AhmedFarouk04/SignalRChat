using EnterpriseChat.Domain.ValueObjects;
using MediatR;

public sealed record PromoteGroupAdminCommand(
    RoomId RoomId,
    UserId TargetUserId,
    UserId RequesterId
) : IRequest<Unit>;
