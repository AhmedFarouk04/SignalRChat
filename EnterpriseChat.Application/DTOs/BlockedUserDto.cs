namespace EnterpriseChat.Application.DTOs;

public sealed record BlockedUserDto(
    Guid UserId,
    string DisplayName,
    DateTime CreatedAt
);
