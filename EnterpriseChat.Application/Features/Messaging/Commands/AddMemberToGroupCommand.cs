using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record AddMemberToGroupCommand(
    RoomId RoomId,
    UserId MemberId,
    UserId RequesterId
) : IRequest;
