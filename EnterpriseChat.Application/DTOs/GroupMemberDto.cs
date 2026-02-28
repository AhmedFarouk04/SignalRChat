namespace EnterpriseChat.Application.DTOs;

public sealed record GroupMemberDto(
    Guid Id,
    string DisplayName,
    bool IsAdmin = false
);
