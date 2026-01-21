using EnterpriseChat.Client.Authentication.Models;

namespace EnterpriseChat.Client.Authentication.Abstractions;

public interface ICurrentUser
{
    Task<AuthUser?> GetAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<Guid?> GetUserIdAsync();
    Task<string?> GetDisplayNameAsync();
}
