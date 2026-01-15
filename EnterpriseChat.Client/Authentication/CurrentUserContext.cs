using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace EnterpriseChat.Client.Authentication;

public sealed class CurrentUserContext
{
    private readonly AuthenticationStateProvider _authProvider;

    public CurrentUserContext(AuthenticationStateProvider authProvider)
    {
        _authProvider = authProvider;
    }

    public async Task<Guid?> GetUserIdAsync()
    {
        var state = await _authProvider.GetAuthenticationStateAsync();
        var user = state.User;

        if (!user.Identity?.IsAuthenticated ?? true)
            return null;

        var sub = user.FindFirst("sub")?.Value;
        if (Guid.TryParse(sub, out var id))
            return id;

        return null;
    }

    public async Task<string?> GetDisplayNameAsync()
    {
        var state = await _authProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst("name")?.Value;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var state = await _authProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated ?? false;
    }
}
