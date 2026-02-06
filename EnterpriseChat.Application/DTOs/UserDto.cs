// EnterpriseChat.Application/DTOs/UserDto.cs
namespace EnterpriseChat.Application.DTOs;

public sealed class UserDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
}