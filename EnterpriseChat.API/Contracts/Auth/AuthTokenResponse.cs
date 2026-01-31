namespace EnterpriseChat.API.Contracts.Auth;

public sealed class AuthTokenResponse
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }

    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
}
