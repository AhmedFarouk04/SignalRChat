using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace EnterpriseChat.Client.Authentication;
public sealed class AuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenService _tokenService;

    public AuthStateProvider(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenService.GetTokenAsync();

        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Mock user (مرحلة أولى)
        var identity = new ClaimsIdentity(
            new[] { new Claim("sub", "11111111-1111-1111-1111-111111111111") },
            "jwt");

        return new AuthenticationState(
            new ClaimsPrincipal(identity));
    }

    public void Notify()
    {
        NotifyAuthenticationStateChanged(
            GetAuthenticationStateAsync());
    }
}
