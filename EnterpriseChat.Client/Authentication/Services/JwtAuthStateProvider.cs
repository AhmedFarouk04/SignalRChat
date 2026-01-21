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

        var identity = BuildIdentityFromJwt(token);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void Notify()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static ClaimsIdentity BuildIdentityFromJwt(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            return new ClaimsIdentity(jwt.Claims, "jwt");
        }
        catch
        {
            return new ClaimsIdentity();
        }
    }
}
