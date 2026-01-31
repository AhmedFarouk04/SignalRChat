namespace EnterpriseChat.API.Auth;

public sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    public string FromName { get; set; } = "EnterpriseChat";
    public string FromEmail { get; set; } = "";
}
