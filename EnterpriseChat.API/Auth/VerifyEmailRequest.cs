namespace EnterpriseChat.API.Auth;

public sealed class VerifyEmailRequest
{
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
}
