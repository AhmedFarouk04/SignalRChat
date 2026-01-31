using System.Text.RegularExpressions;

namespace EnterpriseChat.Domain.Entities;

public sealed class ChatUser
{
    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    // ✅ جديد
    public string Username { get; private set; } = string.Empty;

    // ✅ هنخلي Email إلزامي في النظام الجديد
    public string Email { get; private set; } = string.Empty;

    // ✅ hashed password
    public string PasswordHash { get; private set; } = string.Empty;

    public bool EmailConfirmed { get; private set; }

    // ✅ OTP verification fields
    public string? EmailOtpHash { get; private set; }
    public DateTime? EmailOtpExpiresAtUtc { get; private set; }
    public int EmailOtpAttempts { get; private set; }
    public DateTime? EmailOtpLastSentAtUtc { get; private set; }

    private ChatUser() { }

    public ChatUser(Guid id, string username, string email, string passwordHash)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invalid id.");

        username = (username ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();

        ValidateUsername(username);
        ValidateEmail(email);

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.");

        Id = id;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;

        // displayName default = username
        DisplayName = username;

        EmailConfirmed = false;
    }

    public void SetDisplayName(string displayName)
    {
        displayName = (displayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(displayName)) return;
        if (displayName.Length > 200) displayName = displayName[..200];
        DisplayName = displayName;
    }

    public void SetOtp(string otpHash, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(otpHash))
            throw new ArgumentException("otpHash is required.");

        EmailOtpHash = otpHash;
        EmailOtpExpiresAtUtc = expiresAtUtc;
        EmailOtpAttempts = 0;
        EmailOtpLastSentAtUtc = DateTime.UtcNow;
    }

    public void IncrementOtpAttempts() => EmailOtpAttempts++;

    public void ConfirmEmail()
    {
        EmailConfirmed = true;
        ClearOtp();
    }

    public void ClearOtp()
    {
        EmailOtpHash = null;
        EmailOtpExpiresAtUtc = null;
        EmailOtpAttempts = 0;
        EmailOtpLastSentAtUtc = null;
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.");

        if (email.Length > 256)
            throw new ArgumentException("Email too long.");

        // بسيط وعملي
        if (!email.Contains('@') || !email.Contains('.'))
            throw new ArgumentException("Invalid email.");
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.");

        if (username.Length < 3 || username.Length > 32)
            throw new ArgumentException("Username length must be 3..32.");

        // a-z A-Z 0-9 _ .
        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_\.]+$"))
            throw new ArgumentException("Username contains invalid characters.");
    }
}
