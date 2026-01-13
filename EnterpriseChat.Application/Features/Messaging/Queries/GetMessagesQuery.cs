using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed record GetMessagesQuery(
    RoomId RoomId,
    UserId UserId,
    int Skip,
    int Take
);
