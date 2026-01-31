using System.Net.Http.Json;
using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Contracts.Auth;

namespace EnterpriseChat.Client.Services.Http;

public sealed class AuthApi
{
    private readonly HttpClient _http;
    private readonly ITokenStore _tokenStore;

    public AuthApi(HttpClient http, ITokenStore tokenStore)
    {
        _http = http;
        _tokenStore = tokenStore;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync(ApiEndpoints.Register, req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : body);

        var dto = await res.Content.ReadFromJsonAsync<RegisterResponse>(cancellationToken: ct);
        return dto ?? throw new InvalidOperationException("Invalid register response.");
    }

    public async Task<AuthTokenResponse> VerifyEmailAsync(VerifyEmailRequest req, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync(ApiEndpoints.VerifyEmail, req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : body);

        var dto = await res.Content.ReadFromJsonAsync<AuthTokenResponse>(cancellationToken: ct);
        return dto ?? throw new InvalidOperationException("Invalid verify response.");
    }

    public async Task ResendCodeAsync(string email, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync(ApiEndpoints.ResendCode, new { email }, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : body);
    }

    public async Task<AuthTokenResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync(ApiEndpoints.Login, req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : body);

        var dto = await res.Content.ReadFromJsonAsync<AuthTokenResponse>(cancellationToken: ct);
        return dto ?? throw new InvalidOperationException("Invalid login response.");
    }

    public Task SaveTokenAsync(string token) => _tokenStore.SetAsync(token);
    public Task ClearTokenAsync() => _tokenStore.ClearAsync();
}

