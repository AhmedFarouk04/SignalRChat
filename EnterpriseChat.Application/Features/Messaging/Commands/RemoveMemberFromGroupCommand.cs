using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record RemoveMemberFromGroupCommand(
    RoomId RoomId,
    UserId MemberId,
    UserId RequesterId
) : IRequest<Unit>;
