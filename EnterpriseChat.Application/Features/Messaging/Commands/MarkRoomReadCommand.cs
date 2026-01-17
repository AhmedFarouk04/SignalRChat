using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record MarkRoomReadCommand(
    RoomId RoomId,
    UserId UserId,
    MessageId LastMessageId
);
