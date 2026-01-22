namespace EnterpriseChat.Application.DTOs;

public sealed record UserDirectoryItemDto(
    Guid Id,
    string DisplayName,
    string? Email
);
