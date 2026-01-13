using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record SendMessageCommand(
    RoomId RoomId,
    UserId SenderId,
    string Content
);
