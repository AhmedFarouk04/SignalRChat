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
        _rt.MemberRemoved += (roomId, userId, removerName) =>
            _toasts.Info("Member removed", "A member was removed from the group"); _rt.MessageReceived += OnMessageReceived;
        _rt.RemovedFromRoom += OnRemovedFromRoom;
        _rt.RoomUpdated += OnRoomUpdated;
        _rt.MessageDelivered += OnMessageDelivered;
        _rt.MessageRead += OnMessageRead;
        _rt.MessageDeliveredToAll += OnMessageDeliveredToAll;
        _rt.MessageReadToAll += OnMessageReadToAll;

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
    private void OnMessageDelivered(Guid messageId)
    {
        Console.WriteLine($"[RoomsVM] Delivered event for message: {messageId}");
        UpdateLastMessageStatusIfNeeded(messageId, MessageStatus.Delivered);
    }
    public async Task RefreshLastMessageStatusesAsync()
    {
        Console.WriteLine("[RoomsVM] Refreshing last message statuses after initial join");

        var freshRooms = await _roomService.GetRoomsAsync(); // استعلام واحد فقط

        var currentList = Rooms.ToList();

        for (int i = 0; i < currentList.Count; i++)
        {
            var current = currentList[i];
            var fresh = freshRooms.FirstOrDefault(r => r.Id == current.Id);
            if (fresh != null)
            {
                currentList[i] = new RoomListItemModel
                {
                    Id = current.Id,
                    Name = current.Name,
                    Type = current.Type,
                    OtherUserId = current.OtherUserId,
                    OtherDisplayName = current.OtherDisplayName,
                    IsMuted = current.IsMuted,
                    UnreadCount = fresh.UnreadCount,
                    LastMessageAt = fresh.LastMessageAt,
                    LastMessagePreview = fresh.LastMessagePreview,
                    LastMessageId = fresh.LastMessageId,
                    LastMessageSenderId = fresh.LastMessageSenderId,
                    LastMessageStatus = fresh.LastMessageStatus  // ← التحديث المهم
                };
            }
        }

        Rooms = currentList;
        ApplyFilter();
        NotifyChanged();
    }
    public async Task RefreshRoomStatusesAsync()
    {
        // reload الـ rooms بس عشان نجيب latest LastMessageStatus + UnreadCount
        var freshRooms = await _roomService.GetRoomsAsync();

        var list = Rooms.ToList();
        foreach (var fresh in freshRooms)
        {
            var idx = list.FindIndex(r => r.Id == fresh.Id);
            if (idx >= 0)
            {
                var room = list[idx];
                list[idx] = new RoomListItemModel
                {
                    Id = room.Id,
                    Name = room.Name,
                    Type = room.Type,
                    OtherUserId = room.OtherUserId,
                    OtherDisplayName = room.OtherDisplayName,
                    IsMuted = room.IsMuted,
                    UnreadCount = fresh.UnreadCount,  // تحديث unread
                    LastMessageAt = fresh.LastMessageAt,
                    LastMessagePreview = fresh.LastMessagePreview,
                    LastMessageId = fresh.LastMessageId,
                    LastMessageSenderId = fresh.LastMessageSenderId,
                    LastMessageStatus = fresh.LastMessageStatus  // ← المهم: تحديث الـ status
                };
            }
        }
        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }
    private void OnMessageDeliveredToAll(Guid messageId, Guid senderId)
    {
        Console.WriteLine($"[RoomsVM] DeliveredToAll: {messageId} from sender {senderId}");
        if (senderId == _cachedUserId)
            UpdateLastMessageStatusIfNeeded(messageId, MessageStatus.Delivered);
    }
    // نفس الحاجة لـ Read
    private void OnMessageRead(Guid messageId)
    {
        UpdateLastMessageStatusIfNeeded(messageId, MessageStatus.Read);
    }

  

    private void OnMessageReadToAll(Guid messageId, Guid senderId)
    {
        if (senderId == _cachedUserId)
            UpdateLastMessageStatusIfNeeded(messageId, MessageStatus.Read);
    }

    private void UpdateLastMessageStatusIfNeeded(Guid messageId, MessageStatus newStatus)
    {
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.LastMessageId == messageId);
        if (idx < 0) return;

        var room = list[idx];

        // الـ status meaningful بس لو آخر رسالة مني أنا
        if (room.LastMessageSenderId != _cachedUserId) return;

        list[idx] = new RoomListItemModel
        {
            Id = room.Id,
            Name = room.Name,
            Type = room.Type,
            OtherUserId = room.OtherUserId,
            OtherDisplayName = room.OtherDisplayName,
            IsMuted = room.IsMuted,
            UnreadCount = room.UnreadCount,
            LastMessageAt = room.LastMessageAt,
            LastMessagePreview = room.LastMessagePreview,
            LastMessageId = room.LastMessageId,
            LastMessageSenderId = room.LastMessageSenderId,
            LastMessageStatus = newStatus  // ✅ التحديث الوحيد
        };

        Rooms = list;
        ApplyFilter();

        NotifyChanged();

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

            // احذف الـ try { await _rt.ConnectAsync(); } catch { }

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
        Console.WriteLine($"[RoomsVM] 📥 RoomUpdated RECEIVED! RoomId={upd.RoomId}, Delta={upd.UnreadDelta}");

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == upd.RoomId);
        if (idx < 0)
        {
            Console.WriteLine($"[RoomsVM] Room {upd.RoomId} not found, ignoring");
            return;
        }

        var r = list[idx];
        var isActive = _flags.ActiveRoomId == upd.RoomId;
        var currentUnread = _flags.GetUnread(upd.RoomId);

        int nextUnread;

        // ✅ مهم جداً: التعامل مع Delta=-1
        if (upd.UnreadDelta < 0)
        {
            // Delta سالب = قراءة الروم
            nextUnread = 0;
            Console.WriteLine($"[RoomsVM] 📖 Room marked as read, setting unread=0");
        }
        else if (isActive)
        {
            // الروم مفتوحة = unread=0
            nextUnread = 0;
            Console.WriteLine($"[RoomsVM] Active room, forcing unread=0");
        }
        else
        {
            // رسالة جديدة
            nextUnread = currentUnread + upd.UnreadDelta;
            Console.WriteLine($"[RoomsVM] New message, unread: {currentUnread} + {upd.UnreadDelta} = {nextUnread}");
        }

        // ✅ تأكد من إنها مش سالبة
        nextUnread = Math.Max(0, nextUnread);

        // ✅ حدث الـ Flags Store
        _flags.SetUnread(upd.RoomId, nextUnread);
        Console.WriteLine($"[RoomsVM] Unread count for room {upd.RoomId}: {currentUnread} -> {nextUnread}");

        // ✅ تحديث الـ Room في القائمة
        list[idx] = new RoomListItemModel
        {
            Id = r.Id,
            Name = r.Name,
            Type = r.Type,
            OtherUserId = r.OtherUserId,
            OtherDisplayName = r.OtherDisplayName,
            IsMuted = r.IsMuted,
            UnreadCount = nextUnread,  // ✅ هنا الأهم!
            LastMessageAt = upd.CreatedAt != DateTime.MinValue ? upd.CreatedAt : r.LastMessageAt,
            LastMessagePreview = !string.IsNullOrEmpty(upd.Preview) ? upd.Preview : r.LastMessagePreview,
            LastMessageId = upd.MessageId != Guid.Empty ? upd.MessageId : r.LastMessageId,
            LastMessageSenderId = upd.SenderId != Guid.Empty ? upd.SenderId : r.LastMessageSenderId,
            LastMessageStatus = r.LastMessageStatus
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
