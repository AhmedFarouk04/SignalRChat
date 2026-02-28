using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Http;

namespace EnterpriseChat.Client.Services.Rooms;

public sealed class RoomService : IRoomService
{
    private readonly IApiClient _api;

    public RoomService(IApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<RoomListItemModel>> GetRoomsAsync()
    {
        var dtos = await _api.GetAsync<List<RoomListItemDto>>("api/rooms") ?? new();

        return dtos.Select(d => new RoomListItemModel
        {
            Id = d.Id,
            Name = d.Name ?? "Room",
            Type = d.Type ?? "Group",
            OtherUserId = d.OtherUserId,
            OtherDisplayName = d.OtherDisplayName,
            IsMuted = d.IsMuted,
            UnreadCount = d.UnreadCount,
            LastMessageAt = d.LastMessageAt,
            LastMessagePreview = d.LastMessagePreview,
            LastMessageId = d.LastMessageId,
            LastMessageSenderId = d.LastMessageSenderId,
            LastMessageStatus = d.LastMessageStatus is null ? null : (MessageStatus?)(int)d.LastMessageStatus.Value,
            LastSeenAt = d.LastSeenAt ,
            MemberNames = d.MemberNames ?? new()
        }).ToList();
    }
    public async Task<List<UserModel>> GetGroupMembersAsync(Guid groupId)
    {
        try
        {
            // استخدم HttpClient مباشرة عشان نشوف الاستجابة
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"https://localhost:7188/api/groups/{groupId}/members");
            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Raw API response for group {groupId}: {json}");

            // حاول تحولها
            var members = await _api.GetAsync<List<UserModel>>($"api/groups/{groupId}/members");
            return members ?? new List<UserModel>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RoomService] Error getting group members: {ex.Message}");
            return new List<UserModel>();
        }
    }
    public Task<RoomModel?> GetRoomAsync(Guid roomId)
        => _api.GetAsync<RoomModel>(ApiEndpoints.Room(roomId));
}
