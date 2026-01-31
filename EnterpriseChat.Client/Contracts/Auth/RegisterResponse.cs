namespace EnterpriseChat.Client.Contracts.Auth;

public sealed class RegisterResponse
{
    public Guid? PendingUserId { get; set; }   // optional
    public string Email { get; set; } = "";
}
