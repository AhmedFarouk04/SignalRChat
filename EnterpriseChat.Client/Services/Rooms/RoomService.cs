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

    public async Task<IReadOnlyList<RoomModel>> GetRoomsAsync()
        => await _api.GetAsync<IReadOnlyList<RoomModel>>(ApiEndpoints.Rooms) ?? [];

    public Task<RoomModel?> GetRoomAsync(Guid roomId)
        => _api.GetAsync<RoomModel>(ApiEndpoints.Room(roomId));
}
