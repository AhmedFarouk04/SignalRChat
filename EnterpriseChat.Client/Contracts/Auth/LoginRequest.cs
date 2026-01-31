namespace EnterpriseChat.Client.Contracts.Auth;

public sealed class LoginRequest
{
    public string Identifier { get; set; } = ""; // email or username
    public string Password { get; set; } = "";
}
