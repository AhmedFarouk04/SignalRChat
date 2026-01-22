using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record MarkRoomReadCommand(
    RoomId RoomId,
    UserId UserId,
    MessageId LastMessageId
) : IRequest<Unit>;
