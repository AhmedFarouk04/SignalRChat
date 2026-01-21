namespace EnterpriseChat.Client.Authentication.Models;

public sealed class AuthUser
{
    public Guid Id { get; init; }
    public string? DisplayName { get; init; }
    public bool IsAuthenticated { get; init; }
}
