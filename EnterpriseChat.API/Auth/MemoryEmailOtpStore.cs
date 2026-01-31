using Microsoft.Extensions.Caching.Memory;

namespace EnterpriseChat.API.Auth;

public sealed class MemoryEmailOtpStore : IEmailOtpStore
{
    private readonly IMemoryCache _cache;

    // 10 minutes
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public MemoryEmailOtpStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<string> CreateAsync(Guid pendingUserId, string email, CancellationToken ct = default)
    {
        var code = GenerateOtp();
        var key = CacheKey(pendingUserId);

        _cache.Set(key, new OtpItem(email.Trim().ToLowerInvariant(), code), Ttl);
        return Task.FromResult(code);
    }

    public Task<bool> ValidateAsync(Guid pendingUserId, string email, string code, CancellationToken ct = default)
    {
        var key = CacheKey(pendingUserId);

        if (!_cache.TryGetValue<OtpItem>(key, out var item))
            return Task.FromResult(false);

        if (!string.Equals(item.Email, email.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);

        // fixed compare not needed for 6-digit, but ok:
        var ok = string.Equals(item.Code, (code ?? "").Trim(), StringComparison.Ordinal);
        return Task.FromResult(ok);
    }

    public Task InvalidateAsync(Guid pendingUserId, CancellationToken ct = default)
    {
        _cache.Remove(CacheKey(pendingUserId));
        return Task.CompletedTask;
    }

    private static string CacheKey(Guid id) => $"otp:{id}";

    private static string GenerateOtp()
    {
        // 6-digit numeric
        var n = Random.Shared.Next(0, 1_000_000);
        return n.ToString("D6");
    }

    private sealed record OtpItem(string Email, string Code);
}
