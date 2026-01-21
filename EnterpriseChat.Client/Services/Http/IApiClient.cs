namespace EnterpriseChat.Client.Services.Http;

public interface IApiClient
{
    Task<T?> GetAsync<T>(string url);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest body);
    Task PostAsync(string url);
    Task DeleteAsync(string url);
}
