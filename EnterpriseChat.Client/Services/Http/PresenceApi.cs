using System.Net.Http.Json;
using EnterpriseChat.Client.Services.Http;

namespace EnterpriseChat.Client.Services.Http;

public sealed class PresenceApi
{
    private readonly HttpClient _http;

    public PresenceApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<IsOnlineResponse> IsOnlineAsync(Guid userId)
    {
        // endpoint: api/presence/online/{userId}
        var url = ApiEndpoints.IsOnline(userId);

        var res = await _http.GetFromJsonAsync<IsOnlineResponse>(url);
        return res ?? new IsOnlineResponse { UserId = userId, Online = false };
    }
}

public sealed class IsOnlineResponse
{
    public Guid UserId { get; set; }
    public bool Online { get; set; }
}
