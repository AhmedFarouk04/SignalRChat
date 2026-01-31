namespace EnterpriseChat.API.Auth;

public interface IEmailOtpStore
{
    Task<string> CreateAsync(Guid pendingUserId, string email, CancellationToken ct = default);
    Task<bool> ValidateAsync(Guid pendingUserId, string email, string code, CancellationToken ct = default);
    Task InvalidateAsync(Guid pendingUserId, CancellationToken ct = default);
}
