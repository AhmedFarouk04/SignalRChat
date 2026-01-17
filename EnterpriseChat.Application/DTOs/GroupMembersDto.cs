namespace EnterpriseChat.Application.DTOs;

public sealed class GroupMembersDto
{
    public Guid OwnerId { get; set; }
    public List<GroupMemberDto> Members { get; set; } = new();
}

public sealed class GroupMemberDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
