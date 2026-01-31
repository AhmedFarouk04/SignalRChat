using EnterpriseChat.API.Contracts.Auth;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.API.Auth;

public sealed class AuthService : IAuthService
{
    private readonly ChatDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailSender _email;

    private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(10);
    private const int MaxOtpAttempts = 8;
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);

    public AuthService(ChatDbContext db, JwtTokenService jwt, IPasswordHasher hasher, IEmailSender email)
    {
        _db = db;
        _jwt = jwt;
        _hasher = hasher;
        _email = email;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest req, CancellationToken ct)
    {
        var username = (req.Username ?? "").Trim();
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var password = req.Password ?? "";
        var confirm = req.ConfirmPassword ?? "";

        if (!AuthValidators.IsValidUsername(username))
            throw new AuthException(400, "Username is invalid. Use 3-32 chars: letters/numbers/._");

        if (!AuthValidators.IsValidEmail(email))
            throw new AuthException(400, "Email is invalid.");

        if (!AuthValidators.IsStrongPassword(password))
            throw new AuthException(400, "Password is too weak. Use 8+ chars with upper/lower, number, and symbol.");

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
            throw new AuthException(400, "ConfirmPassword does not match Password.");

        var unameLower = username.ToLowerInvariant();

        // confirmed only -> conflict
        var usernameConfirmedExists = await _db.Users
            .AnyAsync(u => u.EmailConfirmed && u.Username.ToLower() == unameLower, ct);
        if (usernameConfirmedExists) throw new AuthException(409, "Username already exists.");

        var emailConfirmedExists = await _db.Users
            .AnyAsync(u => u.EmailConfirmed && u.Email.ToLower() == email, ct);
        if (emailConfirmedExists) throw new AuthException(409, "Email already exists.");

        // pending reuse logic
        var pending = await _db.Users.FirstOrDefaultAsync(u =>
            !u.EmailConfirmed && (u.Email.ToLower() == email || u.Username.ToLower() == unameLower), ct);

        if (pending is not null)
        {
            // pending for same email => reuse & resend (if cooldown passed)
            if (string.Equals(pending.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                await TrySendOtpAsync(pending, ct);
                return new RegisterResponse { Email = pending.Email };
            }

            // pending username but different email => username is effectively reserved
            throw new AuthException(409, "Username already exists.");
        }

        // create new pending user
        var passwordHash = _hasher.Hash(password);
        var user = new ChatUser(Guid.NewGuid(), username, email, passwordHash);

        var otp = OtpUtils.GenerateOtp();
        user.SetOtp(OtpUtils.HashOtp(otp), DateTime.UtcNow.Add(OtpTtl));

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(
            toEmail: email,
            subject: "EnterpriseChat verification code",
            body: $"Your verification code is: {otp}\nIt expires in 10 minutes.",
            ct: ct);

        return new RegisterResponse { Email = user.Email };
    }

    public async Task<AuthTokenResponse> VerifyEmailAsync(VerifyEmailRequest req, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (!AuthValidators.IsValidEmail(email))
            throw new AuthException(400, "Email is invalid.");

        var code = (req.Code ?? "").Trim();
        if (code.Length != 6)
            throw new AuthException(400, "Invalid code.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        if (user is null) throw new AuthException(404, "User not found.");

        if (user.EmailConfirmed)
            return BuildTokenResponse(user);

        if (user.EmailOtpExpiresAtUtc is null || user.EmailOtpExpiresAtUtc < DateTime.UtcNow)
            throw new AuthException(400, "Code expired. Please request a new code.");

        if (user.EmailOtpAttempts >= MaxOtpAttempts)
            throw new AuthException(400, "Too many attempts. Please request a new code.");

        if (string.IsNullOrWhiteSpace(user.EmailOtpHash))
            throw new AuthException(400, "No code found. Please request a new code.");

        var ok = string.Equals(user.EmailOtpHash, OtpUtils.HashOtp(code), StringComparison.Ordinal);
        if (!ok)
        {
            user.IncrementOtpAttempts();
            await _db.SaveChangesAsync(ct);
            throw new AuthException(400, "Invalid code.");
        }

        // extra safety
        var emailTaken = await _db.Users.AnyAsync(u =>
            u.EmailConfirmed && u.Email.ToLower() == user.Email.ToLower() && u.Id != user.Id, ct);
        if (emailTaken) throw new AuthException(409, "Email already exists.");

        var usernameTaken = await _db.Users.AnyAsync(u =>
            u.EmailConfirmed && u.Username.ToLower() == user.Username.ToLower() && u.Id != user.Id, ct);
        if (usernameTaken) throw new AuthException(409, "Username already exists.");

        user.ConfirmEmail();
        await _db.SaveChangesAsync(ct);

        return BuildTokenResponse(user);
    }

    public async Task<AuthTokenResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var identifier = (req.Identifier ?? "").Trim();
        var password = req.Password ?? "";

        if (string.IsNullOrWhiteSpace(identifier))
            throw new AuthException(400, "Identifier is required.");

        if (string.IsNullOrWhiteSpace(password))
            throw new AuthException(400, "Password is required.");

        var lowered = identifier.ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == lowered || u.Username.ToLower() == lowered, ct);

        if (user is null) throw new AuthException(401, "Invalid credentials.");
        if (!user.EmailConfirmed) throw new AuthException(401, "Email is not verified.");

        if (!_hasher.Verify(password, user.PasswordHash))
            throw new AuthException(401, "Invalid credentials.");

        return BuildTokenResponse(user);
    }

    public async Task ResendCodeAsync(ResendCodeRequest req, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (!AuthValidators.IsValidEmail(email))
            throw new AuthException(400, "Email is invalid.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        if (user is null) throw new AuthException(404, "User not found.");
        if (user.EmailConfirmed) return;

        if (user.EmailOtpLastSentAtUtc is not null &&
            DateTime.UtcNow - user.EmailOtpLastSentAtUtc.Value < ResendCooldown)
            throw new AuthException(400, "Please wait before resending code.");

        await ForceSendOtpAsync(user, ct);
    }

    private async Task TrySendOtpAsync(ChatUser user, CancellationToken ct)
    {
        if (user.EmailOtpLastSentAtUtc is not null &&
            DateTime.UtcNow - user.EmailOtpLastSentAtUtc.Value < ResendCooldown)
            return;

        await ForceSendOtpAsync(user, ct);
    }

    private async Task ForceSendOtpAsync(ChatUser user, CancellationToken ct)
    {
        var code = OtpUtils.GenerateOtp();
        user.SetOtp(OtpUtils.HashOtp(code), DateTime.UtcNow.Add(OtpTtl));
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(
            user.Email,
            "EnterpriseChat verification code",
            $"Your verification code is: {code}\nIt expires in 10 minutes.",
            ct);
    }

    private AuthTokenResponse BuildTokenResponse(ChatUser user)
    {
        var (token, exp) = _jwt.CreateToken(user.Id, user.DisplayName, user.Email);
        return new AuthTokenResponse
        {
            AccessToken = token,
            ExpiresAtUtc = exp,
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email
        };
    }
}
