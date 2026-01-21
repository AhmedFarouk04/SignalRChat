using System.Security.Claims;
using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Authentication.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace EnterpriseChat.Client.Authentication.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly AuthenticationStateProvider _authProvider;

    public CurrentUser(AuthenticationStateProvider authProvider)
    {
        _authProvider = authProvider;
    }

    public async Task<AuthUser?> GetAsync()
    {
        var state = await _authProvider.GetAuthenticationStateAsync();
        var user = state.User;

        var isAuth = user.Identity?.IsAuthenticated ?? false;
        if (!isAuth)
            return null;

        var sub = user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var id))
            return null;

        return new AuthUser
        {
            Id = id,
            DisplayName =
         user.FindFirst("name")?.Value
         ?? user.FindFirst("unique_name")?.Value,
            IsAuthenticated = true
        };

    }

    public async Task<bool> IsAuthenticatedAsync()
        => (await _authProvider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated ?? false;

    public async Task<Guid?> GetUserIdAsync()
        => (await GetAsync())?.Id;

    public async Task<string?> GetDisplayNameAsync()
        => (await GetAsync())?.DisplayName;
}
