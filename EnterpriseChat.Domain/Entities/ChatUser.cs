namespace EnterpriseChat.Domain.Entities;

public sealed class ChatUser
{
    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }

    private ChatUser() { }

    public ChatUser(Guid id, string displayName, string? email = null)
    {
        Id = id;
        DisplayName = displayName.Trim();
        Email = email;
    }
}
