using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record DeliverRoomMessagesCommand(
    RoomId RoomId,
    UserId UserId
) : IRequest;
