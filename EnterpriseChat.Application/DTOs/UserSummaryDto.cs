namespace EnterpriseChat.Application.DTOs;

public sealed class UserSummaryDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
}