namespace EnterpriseChat.Client.Contracts.Auth;

public sealed class VerifyEmailRequest
{
    public Guid? PendingUserId { get; set; }   // optional (لو موجود)
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
}
