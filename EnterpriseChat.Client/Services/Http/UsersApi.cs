using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Services.Http;

namespace EnterpriseChat.Client.Services.Http;

public sealed class UsersApi
{
    private readonly IApiClient _api;

    public UsersApi(IApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<UserDirectoryItemDto>> SearchAsync(
     string query,
     Guid currentUserId,
     int take = 20,
     CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<UserDirectoryItemDto>();

        // ✅ شيل الشرط بتاع Length < 2 برضه
        // if (query.Length < 2) 
        //     return Array.Empty<UserDirectoryItemDto>();

        var res = await _api.GetAsync<IReadOnlyList<UserDirectoryItemDto>>(
            ApiEndpoints.UserSearch(query, take, currentUserId),
            ct);

        return res ?? Array.Empty<UserDirectoryItemDto>();
    }
}