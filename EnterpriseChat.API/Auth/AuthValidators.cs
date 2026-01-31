using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace EnterpriseChat.API.Auth;

public static class AuthValidators
{
    private static readonly Regex UsernameRegex =
        new(@"^[a-zA-Z0-9_\.]+$", RegexOptions.Compiled);

    public static bool IsValidEmail(string email)
        => new EmailAddressAttribute().IsValid(email);

    public static bool IsValidUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        username = username.Trim();
        if (username.Length is < 3 or > 32) return false;
        return UsernameRegex.IsMatch(username);
    }

    public static bool IsStrongPassword(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return false;
        if (p.Length < 8) return false;

        bool hasUpper = p.Any(char.IsUpper);
        bool hasLower = p.Any(char.IsLower);
        bool hasDigit = p.Any(char.IsDigit);
        bool hasSymbol = p.Any(ch => !char.IsLetterOrDigit(ch));

        return hasUpper && hasLower && hasDigit && hasSymbol;
    }
}
