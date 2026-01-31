using EnterpriseChat.API.Contracts.Auth;

namespace EnterpriseChat.API.Auth;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest req, CancellationToken ct);
    Task<AuthTokenResponse> VerifyEmailAsync(VerifyEmailRequest req, CancellationToken ct);
    Task<AuthTokenResponse> LoginAsync(LoginRequest req, CancellationToken ct);
    Task ResendCodeAsync(ResendCodeRequest req, CancellationToken ct);
}
