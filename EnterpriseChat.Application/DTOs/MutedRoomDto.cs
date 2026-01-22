namespace EnterpriseChat.Application.DTOs;

public sealed record MutedRoomDto(
    Guid RoomId,
    DateTime MutedAt
);
