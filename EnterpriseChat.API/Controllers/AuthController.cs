using EnterpriseChat.API.Auth;
using EnterpriseChat.API.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseChat.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest req, CancellationToken ct)
        => Ok(await _auth.RegisterAsync(req, ct));

    [AllowAnonymous]
    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthTokenResponse>> VerifyEmail([FromBody] VerifyEmailRequest req, CancellationToken ct)
        => Ok(await _auth.VerifyEmailAsync(req, ct));

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthTokenResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
        => Ok(await _auth.LoginAsync(req, ct));

    [AllowAnonymous]
    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeRequest req, CancellationToken ct)
    {
        await _auth.ResendCodeAsync(req, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout() => NoContent();
}
