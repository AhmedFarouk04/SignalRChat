namespace EnterpriseChat.Client.Authentication;
public interface ITokenService
{
    Task SetTokenAsync(string token);
    Task<string?> GetTokenAsync();
    Task ClearAsync();
}
