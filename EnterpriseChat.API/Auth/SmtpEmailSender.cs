using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace EnterpriseChat.API.Auth;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _s;

    public SmtpEmailSender(IOptions<SmtpSettings> options)
    {
        _s = options.Value;

        if (string.IsNullOrWhiteSpace(_s.Host))
            throw new InvalidOperationException("SMTP Host is missing (Smtp:Host).");

        if (string.IsNullOrWhiteSpace(_s.Username))
            throw new InvalidOperationException("SMTP Username is missing (Smtp:Username).");

        if (string.IsNullOrWhiteSpace(_s.Password))
            throw new InvalidOperationException("SMTP Password is missing (Smtp:Password).");

        if (string.IsNullOrWhiteSpace(_s.FromEmail))
            throw new InvalidOperationException("SMTP FromEmail is missing (Smtp:FromEmail).");
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("toEmail is required.", nameof(toEmail));

        using var msg = new MailMessage
        {
            From = new MailAddress(_s.FromEmail, string.IsNullOrWhiteSpace(_s.FromName) ? "EnterpriseChat" : _s.FromName),
            Subject = subject ?? "",
            Body = body ?? "",
            IsBodyHtml = false
        };

        msg.To.Add(new MailAddress(toEmail));

        using var client = new SmtpClient(_s.Host, _s.Port)
        {
            EnableSsl = _s.EnableSsl,
            Credentials = new NetworkCredential(_s.Username, _s.Password)
        };

        // SendMailAsync doesn't accept CancellationToken in many versions.
        await client.SendMailAsync(msg);
    }
}
