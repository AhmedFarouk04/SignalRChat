using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseChat.Client.Authentication.Abstractions;

namespace EnterpriseChat.Client.Services.Http;

public sealed class ApiClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenStore _tokenStore;

    public ApiClient(HttpClient http, ITokenStore tokenStore)
    {
        _http = http;
        _tokenStore = tokenStore;
    }

    private async Task AttachTokenAsync()
    {
        var token = await _tokenStore.GetAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        await AttachTokenAsync();
        return await _http.GetFromJsonAsync<T>(url);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest body)
    {
        await AttachTokenAsync();

        var res = await _http.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();

        return await res.Content.ReadFromJsonAsync<TResponse>();
    }

    public async Task PostAsync(string url)
    {
        await AttachTokenAsync();

        var res = await _http.PostAsync(url, null);
        res.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string url)
    {
        await AttachTokenAsync();

        var res = await _http.DeleteAsync(url);
        res.EnsureSuccessStatusCode();
    }
}
