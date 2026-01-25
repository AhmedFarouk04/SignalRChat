using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Rooms;
using EnterpriseChat.Client.Services.Ui;

namespace EnterpriseChat.Client.ViewModels;

public enum RoomsFilter
{
    All,
    Unread,
    Muted,
    Groups,
    Private
}

public sealed class RoomsViewModel
{
    private readonly IRoomService _roomService;
    private readonly ToastService _toasts;

    public RoomsViewModel(IRoomService roomService, ToastService toasts)
    {
        _roomService = roomService;
        _toasts = toasts;
    }

    public IReadOnlyList<RoomListItemModel> Rooms { get; private set; } = Array.Empty<RoomListItemModel>();
    public IReadOnlyList<RoomListItemModel> VisibleRooms { get; private set; } = Array.Empty<RoomListItemModel>();

    public bool IsLoading { get; private set; }
    public bool IsEmpty => !IsLoading && VisibleRooms.Count == 0;

    public string SearchQuery { get; private set; } = "";
    public RoomsFilter ActiveFilter { get; private set; } = RoomsFilter.All;
    public event Action? Changed;
    private void NotifyChanged() => Changed?.Invoke();
    public async Task LoadAsync()
    {
        IsLoading = true;
        NotifyChanged();

        try
        {
            Rooms = await _roomService.GetRoomsAsync();
            ApplyFilter();
        }
        catch
        {
            _toasts.Error("Failed", "Could not load rooms. Check API / token.");
            Rooms = Array.Empty<RoomListItemModel>();
            VisibleRooms = Array.Empty<RoomListItemModel>();
            NotifyChanged();
        }
        finally
        {
            IsLoading = false;
            NotifyChanged();
        }
    }

    public void SetSearch(string q)
    {
        SearchQuery = q ?? "";
        ApplyFilter();
        NotifyChanged();
    }

    public void SetFilter(RoomsFilter filter)
    {
        ActiveFilter = filter;
        ApplyFilter();
        NotifyChanged();
    }

    private void ApplyFilter()
    {
        IEnumerable<RoomListItemModel> q = Rooms;

        // sort by last msg
        q = q.OrderByDescending(r => r.LastMessageAt ?? DateTime.MinValue);

        // filter
        q = ActiveFilter switch
        {
            RoomsFilter.Unread => q.Where(r => r.UnreadCount > 0),
            RoomsFilter.Muted => q.Where(r => r.IsMuted),
            RoomsFilter.Groups => q.Where(r => string.Equals(r.Type, "Group", StringComparison.OrdinalIgnoreCase)),
            RoomsFilter.Private => q.Where(r => string.Equals(r.Type, "Private", StringComparison.OrdinalIgnoreCase)),
            _ => q
        };

        // search
        var s = (SearchQuery ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(s))
        {
            q = q.Where(r =>
                (r.Name?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.OtherDisplayName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.LastMessagePreview?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        VisibleRooms = q.ToList();
    }
}
