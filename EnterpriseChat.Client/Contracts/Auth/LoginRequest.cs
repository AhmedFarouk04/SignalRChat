namespace EnterpriseChat.Client.Contracts.Auth;

public sealed class LoginRequest
{
    public string Identifier { get; set; } = "";     public string Password { get; set; } = "";
}
