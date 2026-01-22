namespace EnterpriseChat.Application.DTOs;

public sealed record UserSummaryDto(
    Guid Id,
    string DisplayName
);
