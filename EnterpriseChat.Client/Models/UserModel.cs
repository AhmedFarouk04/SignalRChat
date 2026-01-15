namespace EnterpriseChat.Client.Models;

public sealed class UserModel
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool IsOnline { get; set; }
}
