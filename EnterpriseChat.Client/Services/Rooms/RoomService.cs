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
        var dtos = await _api.GetAsync<IReadOnlyList<RoomListItemDto>>(ApiEndpoints.Rooms) ?? [];
        return dtos.Select(d => new RoomListItemModel
        {
            Id = d.Id,
            Name = d.Name,
            Type = d.Type,
            OtherUserId = d.OtherUserId,
            OtherDisplayName = d.OtherDisplayName,
            UnreadCount = d.UnreadCount,
            IsMuted = d.IsMuted,
            LastMessageAt = d.LastMessageAt,
            LastMessagePreview = d.LastMessagePreview,
            LastMessageId = d.LastMessageId,

            // ✅ NEW (هنا بالظبط)
            LastMessageSenderId = d.LastMessageSenderId,
            LastMessageStatus = d.LastMessageStatus.HasValue
         ? (EnterpriseChat.Client.Models.MessageStatus)d.LastMessageStatus.Value
         : null,
        }).ToList();

    }
    public Task<RoomModel?> GetRoomAsync(Guid roomId)
        => _api.GetAsync<RoomModel>(ApiEndpoints.Room(roomId));
}
