namespace EnterpriseChat.Application.DTOs;

public sealed record GroupMemberDetailsDto(
    Guid UserId,
    string DisplayName,
    bool IsOwner,
    bool IsAdmin,
    DateTime JoinedAt
);
