using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Rooms;

namespace EnterpriseChat.Client.ViewModels;

public sealed class RoomsViewModel
{
    private readonly IRoomService _roomService;

    public RoomsViewModel(IRoomService roomService)
    {
        _roomService = roomService;
    }

    public IReadOnlyList<RoomModel>? Rooms { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsEmpty => Rooms != null && Rooms.Count == 0;
    public string? Error { get; private set; }
    public string? UiError { get; private set; }
    public async Task LoadAsync()
    {
        IsLoading = true;
        UiError = null;

        try
        {
            Rooms = await _roomService.GetRoomsAsync();
        }
        catch
        {
            UiError = "Failed to load rooms.";
        }
        finally
        {
            IsLoading = false;
        }
    }

}
