namespace EnterpriseChat.Application.DTOs;

public sealed record GroupDetailsDto(
    Guid RoomId,
    string Name,
    Guid? OwnerId,
    DateTime CreatedAt,
    IReadOnlyList<GroupMemberDetailsDto> Members
);