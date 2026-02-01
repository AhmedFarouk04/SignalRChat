using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Chat;
using EnterpriseChat.Client.Services.Realtime;
using EnterpriseChat.Client.Services.Rooms;
using EnterpriseChat.Client.Services.Ui;

namespace EnterpriseChat.Client.ViewModels;

public enum RoomsFilter { All, Unread, Muted, Groups, Private }

public sealed class RoomsViewModel
{
    private readonly IRoomService _roomService;
    private readonly ToastService _toasts;
    private readonly IChatRealtimeClient _rt;
    private readonly RoomFlagsStore _flags;
    private readonly NotificationSoundService _sound;
    private readonly IChatService _chatService; // جديد
    private readonly ICurrentUser _currentUser; // جديد، عشان نعرف id بتاعنا
    private readonly Dictionary<Guid, Guid> _lastUpdateMessageByRoom = new();
    private Guid _cachedUserId; // بدون ? عشان هيبقى set دايمًا بعد Load
    private bool _userIdCached = false; // flag عشان لو Load فشل
    public RoomsViewModel(
        IRoomService roomService,
        ToastService toasts,
        IChatRealtimeClient rt,
        RoomFlagsStore flags,
        NotificationSoundService sound,
        IChatService chatService, 
    ICurrentUser currentUser)
    {
        _roomService = roomService;
        _toasts = toasts;
        _rt = rt;
        _flags = flags;
        _sound = sound;
        _chatService = chatService;
        _currentUser = currentUser;
        _flags.RoomUnreadChanged += OnRoomUnreadChanged;

        // ✅ جديد: لما تدخل/تخرج من روم
        _flags.ActiveRoomChanged += OnActiveRoomChanged;

        _rt.MessageReceived += OnMessageReceived;
        _rt.RoomUpdated += OnRoomUpdated;
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

            foreach (var r in Rooms)
                _flags.SetUnread(r.Id, r.UnreadCount);

            try { await _rt.ConnectAsync(); } catch { }

            var userId = await _currentUser.GetUserIdAsync();
            if (userId.HasValue)
            {
                _cachedUserId = userId.Value;
                _userIdCached = true;
            }

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

    private void OnMessageReceived(MessageModel msg)
    {
        // لو أنا فاتح نفس الروم: مفيش إشعار
        if (_flags.ActiveRoomId == msg.RoomId) return;

        // لو Muted: لا صوت
        if (_flags.GetMuted(msg.RoomId)) return;

        // ✅ صوت الإشعار فقط
        _ = Task.Run(async () =>
        {
            try
            {
                var ok = await _sound.PlayAsync();
                if (!ok) Console.WriteLine("[notify] play blocked (need unlock?)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[notify] error: " + ex.Message);
            }
        });
    }

    // ✅ جديد: أول ما الروم تبقى Active صَفّر unread فورًا (بدون refresh)
    private void OnActiveRoomChanged(Guid? roomId)
    {
        if (roomId is null) return;

        // ✅ صفر في store (مصدر الحقيقة)
        _flags.SetUnread(roomId.Value, 0);

        // هيتعمل update للـ Rooms عن طريق OnRoomUnreadChanged
    }

    private void OnRoomUnreadChanged(Guid roomId)
    {
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        var r = list[idx];
        var nextUnread = _flags.GetUnread(roomId);

        list[idx] = new RoomListItemModel
        {
            Id = r.Id,
            Name = r.Name,
            Type = r.Type,
            OtherUserId = r.OtherUserId,
            OtherDisplayName = r.OtherDisplayName,
            IsMuted = r.IsMuted,
            UnreadCount = nextUnread,
            LastMessageAt = r.LastMessageAt,
            LastMessagePreview = r.LastMessagePreview,
            LastMessageId = r.LastMessageId
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }

    private async void OnRoomUpdated(RoomUpdatedModel upd)
    {
        Console.WriteLine($"[RoomsVM] RoomUpdated RECEIVED: RoomId={upd.RoomId}, Delta={upd.UnreadDelta}, MessageId={upd.MessageId}, Preview='{upd.Preview}', Sender={upd.SenderId}");

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == upd.RoomId);
        if (idx < 0) return;
        if (_lastUpdateMessageByRoom.TryGetValue(upd.RoomId, out var lastMsg) && lastMsg == upd.MessageId)
            return;

        _lastUpdateMessageByRoom[upd.RoomId] = upd.MessageId;

        var r = list[idx];

        // ✅ لو أنا جوه نفس الشات: unread لازم يفضل 0
        var isActive = _flags.ActiveRoomId == upd.RoomId;

        // مصدر الحقيقة: store (مش r.UnreadCount لأن ده ممكن يكون stale)
        var currentUnread = _flags.GetUnread(upd.RoomId);

        var nextUnread =
            upd.UnreadDelta < 0 ? 0 :
            (isActive ? 0 : Math.Max(0, currentUnread + upd.UnreadDelta));

        // ✅ حدّث الستور الأول
        _flags.SetUnread(upd.RoomId, nextUnread);

        // ✅ جديد: mark delivered للرسالة الجديدة لو delta >0 (يعني رسالة من غيري)
        // ✅ جديد: استخدم cached id (بدون await)
        if (upd.UnreadDelta > 0 && upd.MessageId != Guid.Empty && _userIdCached && upd.SenderId != _cachedUserId)
        {
            _ = Task.Run(async () =>
            {
                try { await _chatService.MarkMessageDeliveredAsync(upd.MessageId); }
                catch { /* silent */ }
            });
        }

        // ✅ حدّث القائمة
        list[idx] = new RoomListItemModel
        {
            Id = r.Id,
            Name = r.Name,
            Type = r.Type,
            OtherUserId = r.OtherUserId,
            OtherDisplayName = r.OtherDisplayName,
            IsMuted = r.IsMuted,
            UnreadCount = nextUnread,
            LastMessageAt = upd.CreatedAt,
            LastMessagePreview = upd.Preview,
            LastMessageId = upd.MessageId
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }

    private void ApplyFilter()
    {
        IEnumerable<RoomListItemModel> q = Rooms;

        q = q.OrderByDescending(r => r.LastMessageAt ?? DateTime.MinValue);

        q = ActiveFilter switch
        {
            RoomsFilter.Unread => q.Where(r => r.UnreadCount > 0),
            RoomsFilter.Muted => q.Where(r => r.IsMuted),
            RoomsFilter.Groups => q.Where(r => string.Equals(r.Type, "Group", StringComparison.OrdinalIgnoreCase)),
            RoomsFilter.Private => q.Where(r => string.Equals(r.Type, "Private", StringComparison.OrdinalIgnoreCase)),
            _ => q
        };

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

    // ✅ لو محتاجها في أماكن تانية (مش لازم مع ActiveRoomChanged)
    public void MarkRoomAsReadLocal(Guid roomId, Guid? lastMessageId = null)
    {
        // صفر store
        _flags.SetUnread(roomId, 0);

        // update preview/lastMessageId (اختياري)
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        var r = list[idx];
        list[idx] = new RoomListItemModel
        {
            Id = r.Id,
            Name = r.Name,
            Type = r.Type,
            OtherUserId = r.OtherUserId,
            OtherDisplayName = r.OtherDisplayName,
            IsMuted = r.IsMuted,
            UnreadCount = 0,
            LastMessageAt = r.LastMessageAt,
            LastMessagePreview = r.LastMessagePreview,
            LastMessageId = lastMessageId ?? r.LastMessageId
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }
}
