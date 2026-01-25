using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EnterpriseChat.Client.Authentication.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace EnterpriseChat.Client.Authentication.Services;

public sealed class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStore _tokenStore;

    public JwtAuthStateProvider(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStore.GetAsync();

        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var identity = await BuildIdentityFromJwtOrEmptyAsync(token);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void Notify() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private async Task<ClaimsIdentity> BuildIdentityFromJwtOrEmptyAsync(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            // Expiration safety (UTC)
            if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo < DateTime.UtcNow)
            {
                await _tokenStore.ClearAsync();
                return new ClaimsIdentity();
            }

            return new ClaimsIdentity(jwt.Claims, "jwt");
        }
        catch
        {
            await _tokenStore.ClearAsync();
            return new ClaimsIdentity();
        }
    }
}
