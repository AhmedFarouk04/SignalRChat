namespace EnterpriseChat.API.Contracts.Auth;

public sealed class LoginRequest
{
    // email OR username
    public string Identifier { get; set; } = "";
    public string Password { get; set; } = "";
}
