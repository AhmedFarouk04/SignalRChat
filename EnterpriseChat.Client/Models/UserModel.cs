namespace EnterpriseChat.Client.Models;

public sealed class UserModel
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;

    public string? Email { get; init; }  // ✅ add this

    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    public bool IsAdmin { get; set; } = false;
}
