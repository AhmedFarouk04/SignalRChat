using EnterpriseChat.Application.DTOs;
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
        _rt.GroupRenamed += OnGroupRenamed; // ✅
        _rt.RoomUpserted += OnRoomUpserted;
        _rt.MemberAdded += OnMemberAddedRealtime;
        _rt.MemberRemoved += OnMemberRemovedRealtime;
        _rt.MessageReceived += OnMessageReceived;
        _rt.RemovedFromRoom += OnRemovedFromRoom;
        _rt.RoomUpdated += OnRoomUpdated;

    }
    private void OnRemovedFromRoom(Guid roomId)
    {
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        list.RemoveAt(idx);
        Rooms = list;

        ApplyFilter();
        NotifyChanged();

        _toasts.Info("Removed", "You were removed from a room.");
    }
    private void OnMemberRemovedRealtime(Guid roomId, Guid userId)
    {
        _toasts.Info("Member removed", "A member was removed from the group");
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
    private void OnRoomUpserted(RoomListItemDto dto)
    {
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == dto.Id);

        // map dto -> RoomListItemModel
        var model = new RoomListItemModel
        {
            Id = dto.Id,
            Name = dto.Name ?? "Room",
            Type = dto.Type ?? "Group",
            OtherUserId = dto.OtherUserId,
            OtherDisplayName = dto.OtherDisplayName,
            UnreadCount = dto.UnreadCount,
            IsMuted = dto.IsMuted,
            LastMessageAt = dto.LastMessageAt,
            LastMessagePreview = dto.LastMessagePreview,
            LastMessageId = dto.LastMessageId,
            LastMessageSenderId = dto.LastMessageSenderId,
            LastMessageStatus = dto.LastMessageStatus is null
   ? null
    : (MessageStatus?)(int)dto.LastMessageStatus.Value
        };

        if (idx >= 0) list[idx] = model;
        else list.Insert(0, model); // ✅ تظهر فوق فورًا

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }
    private void OnMemberAddedRealtime(Guid roomId, Guid userId, string displayName)
       => _toasts.Success("Member added", $"{displayName} joined");

   



  


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

    private void OnGroupRenamed(Guid roomId, string newName)
    {
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        var r = list[idx];
        list[idx] = new RoomListItemModel
        {
            Id = r.Id,
            Name = newName,
            Type = r.Type,
            OtherUserId = r.OtherUserId,
            OtherDisplayName = r.OtherDisplayName,
            IsMuted = r.IsMuted,
            UnreadCount = r.UnreadCount,
            LastMessageAt = r.LastMessageAt,
            LastMessagePreview = r.LastMessagePreview,
            LastMessageId = r.LastMessageId
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
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
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == upd.RoomId);

        // ✅ لو الروم مش موجودة: ضيفها
        if (idx < 0)
        {
            string name = upd.RoomName ?? "";
            string type = upd.RoomType ?? "";

            // لو السيرفر مبعتش الاسم/النوع، هاتهم من API
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
            {
                try
                {
                    var room = await _roomService.GetRoomAsync(upd.RoomId);
                    if (room != null)
                    {
                        name = string.IsNullOrWhiteSpace(name) ? room.Name : name;
                        type = string.IsNullOrWhiteSpace(type) ? room.Type : type;
                    }
                }
                catch { }
            }

            list.Insert(0, new RoomListItemModel
            {
                Id = upd.RoomId,
                Name = string.IsNullOrWhiteSpace(name) ? "Room" : name,
                Type = string.IsNullOrWhiteSpace(type) ? "Group" : type,
                UnreadCount = 0,
                IsMuted = _flags.GetMuted(upd.RoomId),
                LastMessageAt = upd.CreatedAt,
                LastMessagePreview = upd.Preview,
                LastMessageId = (upd.MessageId == Guid.Empty ? null : upd.MessageId)
            });

            Rooms = list;
            ApplyFilter();
            NotifyChanged();
            return;
        }

        // باقي كودك زي ما هو (duplicate check + unread logic + update room)
        if (_lastUpdateMessageByRoom.TryGetValue(upd.RoomId, out var lastMsg) && lastMsg == upd.MessageId)
            return;

        _lastUpdateMessageByRoom[upd.RoomId] = upd.MessageId;

        var r = list[idx];
        var isActive = _flags.ActiveRoomId == upd.RoomId;
        var currentUnread = _flags.GetUnread(upd.RoomId);

        var nextUnread =
            upd.UnreadDelta < 0 ? 0 :
            (isActive ? 0 : Math.Max(0, currentUnread + upd.UnreadDelta));

        _flags.SetUnread(upd.RoomId, nextUnread);

        if (upd.UnreadDelta > 0 && upd.MessageId != Guid.Empty && _userIdCached && upd.SenderId != _cachedUserId)
        {
            _ = Task.Run(async () =>
            {
                try { await _chatService.MarkMessageDeliveredAsync(upd.MessageId); } catch { }
            });
        }

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
