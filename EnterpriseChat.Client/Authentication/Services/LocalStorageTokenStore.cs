using EnterpriseChat.Client.Authentication.Abstractions;
using Microsoft.JSInterop;

namespace EnterpriseChat.Client.Authentication.Services;

public sealed class LocalStorageTokenStore : ITokenStore
{
    private const string Key = "auth_token";
    private readonly IJSRuntime _js;

    public LocalStorageTokenStore(IJSRuntime js)
    {
        _js = js;
    }

    public Task SetAsync(string token)
        => _js.InvokeVoidAsync("localStorage.setItem", Key, token).AsTask();

    public async Task<string?> GetAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", Key);

    public Task ClearAsync()
        => _js.InvokeVoidAsync("localStorage.removeItem", Key).AsTask();
}
