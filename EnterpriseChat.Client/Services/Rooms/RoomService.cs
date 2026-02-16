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
            LastSeenAt = d.LastSeenAt // ➕ أضف هذا السطر
        }).ToList();
    }
    public Task<RoomModel?> GetRoomAsync(Guid roomId)
        => _api.GetAsync<RoomModel>(ApiEndpoints.Room(roomId));
}
