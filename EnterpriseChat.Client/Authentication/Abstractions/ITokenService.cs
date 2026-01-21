namespace EnterpriseChat.Client.Authentication.Abstractions;

public interface ITokenStore
{
    Task SetAsync(string token);
    Task<string?> GetAsync();
    Task ClearAsync();
}
