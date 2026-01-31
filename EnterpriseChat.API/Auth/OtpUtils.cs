using System.Security.Cryptography;
using System.Text;

namespace EnterpriseChat.API.Auth;

public static class OtpUtils
{
    public static string GenerateOtp()
        => Random.Shared.Next(0, 1_000_000).ToString("D6");

    public static string HashOtp(string otp)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes);
    }
}
