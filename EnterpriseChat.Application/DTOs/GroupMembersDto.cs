namespace EnterpriseChat.Application.DTOs;

public sealed record GroupMembersDto(
    Guid OwnerId,
    IReadOnlyList<GroupMemberDto> Members
);
