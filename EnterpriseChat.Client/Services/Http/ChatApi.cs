using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Services.Http;

namespace EnterpriseChat.Client.Services.Http;

public sealed class ChatApi
{
    private readonly IApiClient _api;

    public ChatApi(IApiClient api)
    {
        _api = api;
    }

    // POST api/chat/private/{userId}
    public Task<PrivateRoomDto?> GetOrCreatePrivateAsync(Guid userId, CancellationToken ct = default)
        => _api.PostAsync<object, PrivateRoomDto>(ApiEndpoints.GetOrCreatePrivate(userId), new { }, ct);
}
