namespace EnterpriseChat.API.Auth;

public sealed class AuthException : Exception
{
    public int StatusCode { get; }

    public AuthException(int statusCode, string message) : base(message)
        => StatusCode = statusCode;
}
