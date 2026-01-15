using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
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

        if (string.IsNullOrWhiteSpace(token))
        {
            return new AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var identity = new ClaimsIdentity(
            jwt.Claims,
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
