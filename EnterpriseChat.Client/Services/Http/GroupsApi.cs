using EnterpriseChat.Application.DTOs;

namespace EnterpriseChat.Client.Services.Http;

public sealed class GroupsApi
{
    private readonly IApiClient _api;

    public GroupsApi(IApiClient api)
    {
        _api = api;
    }

    // ✅ matches swagger body:
    // { "name": "...", "members": ["guid", ...] }
    public sealed class CreateGroupRequest
    {
        public string Name { get; set; } = "";
        public List<Guid> Members { get; set; } = new();
    }

    public sealed class UpdateGroupRequest
    {
        public string Name { get; set; } = "";
    }

    public Task<GroupDetailsDto?> GetGroupAsync(Guid roomId, CancellationToken ct = default)
        => _api.GetAsync<GroupDetailsDto>(ApiEndpoints.GetGroup(roomId), ct);

    public async Task<GroupDetailsDto> CreateGroupAsync(CreateGroupRequest req, CancellationToken ct = default)
    {
        var dto = await _api.PostAsync<CreateGroupRequest, GroupDetailsDto>(ApiEndpoints.Groups, req, ct);
        return dto ?? throw new InvalidOperationException("Invalid create group response.");
    }

    public Task UpdateGroupAsync(Guid roomId, string name, CancellationToken ct = default)
        => _api.PutAsync(ApiEndpoints.UpdateGroup(roomId), new UpdateGroupRequest { Name = name }, ct);

    public Task DeleteGroupAsync(Guid roomId, CancellationToken ct = default)
        => _api.DeleteAsync(ApiEndpoints.DeleteGroup(roomId), ct);

    public Task LeaveGroupAsync(Guid roomId, CancellationToken ct = default)
    => _api.DeleteAsync(ApiEndpoints.LeaveGroup(roomId), ct);

    public Task<GroupMembersDto?> GetMembersAsync(Guid roomId, CancellationToken ct = default)
        => _api.GetAsync<GroupMembersDto>(ApiEndpoints.GroupMembers(roomId), ct);

    public Task AddMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
        => _api.PostAsync(ApiEndpoints.AddGroupMember(roomId, userId), ct);

    public Task RemoveMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
        => _api.DeleteAsync(ApiEndpoints.RemoveGroupMember(roomId, userId), ct);

    public Task PromoteAdminAsync(Guid roomId, Guid userId, CancellationToken ct = default)
        => _api.PostAsync(ApiEndpoints.PromoteAdmin(roomId, userId), ct);

    public Task DemoteAdminAsync(Guid roomId, Guid userId, CancellationToken ct = default)
        => _api.DeleteAsync(ApiEndpoints.DemoteAdmin(roomId, userId), ct);

    public Task TransferOwnerAsync(Guid roomId, Guid userId, CancellationToken ct = default)
        => _api.PostAsync(ApiEndpoints.TransferOwner(roomId, userId), ct);
}
