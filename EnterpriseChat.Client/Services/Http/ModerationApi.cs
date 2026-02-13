using EnterpriseChat.Application.DTOs;

namespace EnterpriseChat.Client.Services.Http;

public sealed class ModerationApi
{
    private readonly IApiClient _api;

    public ModerationApi(IApiClient api)
    {
        _api = api;
    }

    // Blocked
    public async Task<IReadOnlyList<BlockedUserDto>> GetBlockedAsync(CancellationToken ct = default)
        => await _api.GetAsync<IReadOnlyList<BlockedUserDto>>(ApiEndpoints.Blocked, ct) ?? Array.Empty<BlockedUserDto>();

    public Task UnblockAsync(Guid userId, CancellationToken ct = default)
        => _api.DeleteAsync(ApiEndpoints.Unblock(userId), ct);

    // Muted
    public async Task<IReadOnlyList<MutedRoomDto>> GetMutedAsync(CancellationToken ct = default)
        => await _api.GetAsync<IReadOnlyList<MutedRoomDto>>(ApiEndpoints.Muted, ct) ?? Array.Empty<MutedRoomDto>();

    public Task UnmuteAsync(Guid roomId, CancellationToken ct = default)
        => _api.DeleteAsync(ApiEndpoints.Unmute(roomId), ct);
    public Task BlockAsync(Guid userId, CancellationToken ct = default)
    => _api.PostAsync<EmptyRequest, object>(ApiEndpoints.Block(userId), new EmptyRequest(), ct);

    public sealed record EmptyRequest;
    // اليوزرز اللي عملوا بلوك للمستخدم الحالي (اللي بيعمل بلوك ليا)
    public async Task<IReadOnlyList<BlockedUserDto>> GetBlockedByMeAsync(CancellationToken ct = default)
    {
        // نفس النمط اللي مستخدم في GetBlockedAsync
        return await _api.GetAsync<IReadOnlyList<BlockedUserDto>>(
            "api/moderation/blocked-by-me",  // ← ده الـ endpoint الجديد اللي هتعمله في الـ backend
            ct
        ) ?? Array.Empty<BlockedUserDto>();
    }

}
