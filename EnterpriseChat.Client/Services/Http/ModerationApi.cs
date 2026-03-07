using EnterpriseChat.Application.DTOs;

namespace EnterpriseChat.Client.Services.Http;

public sealed class ModerationApi
{
    private readonly IApiClient _api;

    public ModerationApi(IApiClient api)
    {
        _api = api;
    }

        public async Task<IReadOnlyList<BlockedUserDto>> GetBlockedAsync(CancellationToken ct = default)
        => await _api.GetAsync<IReadOnlyList<BlockedUserDto>>(ApiEndpoints.Blocked, ct) ?? Array.Empty<BlockedUserDto>();

    public Task UnblockAsync(Guid userId, CancellationToken ct = default)
        => _api.DeleteAsync(ApiEndpoints.Unblock(userId), ct);

        public async Task<IReadOnlyList<MutedRoomDto>> GetMutedAsync(CancellationToken ct = default)
        => await _api.GetAsync<IReadOnlyList<MutedRoomDto>>(ApiEndpoints.Muted, ct) ?? Array.Empty<MutedRoomDto>();

    public Task UnmuteAsync(Guid roomId, CancellationToken ct = default)
        => _api.DeleteAsync(ApiEndpoints.Unmute(roomId), ct);
    public Task BlockAsync(Guid userId, CancellationToken ct = default)
    => _api.PostAsync<EmptyRequest, object>(ApiEndpoints.Block(userId), new EmptyRequest(), ct);

    public sealed record EmptyRequest;
        public async Task<IReadOnlyList<BlockedUserDto>> GetBlockedByMeAsync(CancellationToken ct = default)
    {
                return await _api.GetAsync<IReadOnlyList<BlockedUserDto>>(
            "api/moderation/blocked-by-me",              ct
        ) ?? Array.Empty<BlockedUserDto>();
    }

}
