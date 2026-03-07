namespace EnterpriseChat.Client.Contracts.Auth;

public sealed class RegisterResponse
{
    public Guid? PendingUserId { get; set; }       public string Email { get; set; } = "";
}
