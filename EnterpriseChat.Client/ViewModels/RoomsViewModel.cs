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
    private readonly IChatService _chatService;
    private readonly ICurrentUser _currentUser;
    private Guid _cachedUserId;
    public Guid CurrentUserId;
    private readonly Dictionary<Guid, bool> _typingStatus = new();

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
        _flags.ActiveRoomChanged += OnActiveRoomChanged;
        _rt.TypingStarted += OnTypingStarted;
        _rt.TypingStopped += OnTypingStopped;
        _rt.GroupRenamed += OnGroupRenamed;
        _rt.RoomUpserted += OnRoomUpserted;
        _rt.MemberAdded += OnMemberAddedRealtime;
        _rt.MemberRemoved += (roomId, userId, removerName) =>
            _toasts.Info("Member removed", "A member was removed from the group");
        _rt.MessageReceived += OnMessageReceived;
        _rt.RemovedFromRoom += OnRemovedFromRoom;
        _rt.RoomUpdated += OnRoomUpdated;
        _rt.MessageReceiptStatsUpdated += OnMessageReceiptStatsUpdated;
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

    private void OnMessageReceiptStatsUpdated(Guid messageId, Guid roomId, int total, int delivered, int read)
    {
        Console.WriteLine($"[RoomsVM] StatsUpdated for room {roomId}, msg {messageId}, d={delivered}, r={read}");

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        var room = list[idx];

        // ✅ تأكد إن ده نفس آخر رسالة في الروم
        if (room.LastMessageId != messageId) return;

        // ✅ حساب الحالة الجديدة
        var newStatus =
            (read >= total && total > 0) ? MessageStatus.Read :
            (delivered >= 1) ? MessageStatus.Delivered :
            MessageStatus.Sent;

        // ✅ الأهم: لو في Manual Update أحدث في الكاش، نفضل الـ Manual Update
        if (_lastMessageStatusCache.TryGetValue(roomId, out var cached) && cached.messageId == messageId)
        {
            if (cached.status > newStatus)
            {
                Console.WriteLine($"[RoomsVM] Using cached status {cached.status} instead of {newStatus}");
                newStatus = cached.status;
            }
        }

        // ✅ منع التحديث للخلف
        if (room.LastMessageStatus.HasValue && newStatus < room.LastMessageStatus.Value)
        {
            Console.WriteLine($"[RoomsVM] Ignoring backward status update: {room.LastMessageStatus} -> {newStatus}");
            return;
        }

        // ✅ لو الحالة اتغيرت (لأعلى)، حدث
        if (room.LastMessageStatus != newStatus)
        {
            Console.WriteLine($"[RoomsVM] Updating last message status for room {roomId}: {room.LastMessageStatus} -> {newStatus}");

            // ✅ خزن في الكاش
            _lastMessageStatusCache[roomId] = (messageId, newStatus);

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
                LastMessageStatus = newStatus
            };

            Rooms = list;
            ApplyFilter();
            NotifyChanged();
        }
    }    // في RoomsViewModel.cs - أضف هذه الدالة
    public void UpdateLastMessageStatus(Guid roomId, Guid messageId, MessageStatus status)
    {
        Console.WriteLine($"[RoomsVM] 🔔 MANUAL UPDATE: room={roomId}, msg={messageId}, status={status}");

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0)
        {
            Console.WriteLine($"[RoomsVM] Room {roomId} not found");
            return;
        }

        var room = list[idx];
        Console.WriteLine($"[RoomsVM] Current room status: LastMessageId={room.LastMessageId}, CurrentStatus={room.LastMessageStatus}");

        // ✅ تأكد إن ده نفس آخر رسالة
        if (room.LastMessageId != messageId)
        {
            Console.WriteLine($"[RoomsVM] Message ID mismatch: room has {room.LastMessageId}, updating with {messageId}");
            // حتى لو mismatch، لسه بنحدث لو هي دي آخر رسالة فعلاً
        }

        // ✅ منع downgrade
        if (room.LastMessageStatus.HasValue && status < room.LastMessageStatus.Value)
        {
            Console.WriteLine($"[RoomsVM] Ignoring manual downgrade: {room.LastMessageStatus} -> {status}");
            return;
        }

        // ✅ خزن في الكاش
        _lastMessageStatusCache[roomId] = (messageId, status);

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
            LastMessageStatus = status  // ✅ التحديث المهم
        };

        Rooms = list;
        ApplyFilter();

        // ✅ Force UI update
        NotifyChanged();
        Console.WriteLine($"[RoomsVM] ✅ Manual update complete for room {roomId}, new status={status}");
    }
    public IReadOnlyList<RoomListItemModel> Rooms { get; private set; } = Array.Empty<RoomListItemModel>();
    public IReadOnlyList<RoomListItemModel> VisibleRooms { get; private set; } = Array.Empty<RoomListItemModel>();
    public bool IsLoading { get; private set; }
    public bool IsEmpty => !IsLoading && VisibleRooms.Count == 0;
    public string SearchQuery { get; private set; } = "";
    public RoomsFilter ActiveFilter { get; private set; } = RoomsFilter.All;
    public event Action? Changed;
    private void NotifyChanged() => Changed?.Invoke();
    private readonly Dictionary<Guid, (Guid messageId, MessageStatus status)> _lastMessageStatusCache = new();

    public async Task LoadAsync()
    {
        IsLoading = true;
        NotifyChanged();
        try
        {
            var userId = await _currentUser.GetUserIdAsync();
            if (!userId.HasValue)
                throw new InvalidOperationException("User not authenticated");

            _cachedUserId = userId.Value;
            CurrentUserId = userId.Value;
            Rooms = await _roomService.GetRoomsAsync();

            foreach (var r in Rooms)
            {
                _flags.SetUnread(r.Id, r.UnreadCount);

                // ✅ استرجاع الحالة المحفوظة
                var savedStatus = _flags.GetLastMessageStatus(r.Id);
                if (savedStatus.HasValue && r.LastMessageStatus != savedStatus.Value)
                {
                    // تحديث الحالة في الـ Room
                    var list = Rooms.ToList();
                    var idx = list.FindIndex(x => x.Id == r.Id);
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
                            UnreadCount = room.UnreadCount,
                            LastMessageAt = room.LastMessageAt,
                            LastMessagePreview = room.LastMessagePreview,
                            LastMessageId = room.LastMessageId,
                            LastMessageSenderId = room.LastMessageSenderId,
                            LastMessageStatus = savedStatus.Value
                        };
                        Rooms = list;
                    }
                }
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
    public async Task RefreshLastMessageStatusesAsync()
    {
        Console.WriteLine("[RoomsVM] Refreshing last message statuses after initial join");
        var freshRooms = await _roomService.GetRoomsAsync();
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
                    LastMessageStatus = fresh.LastMessageStatus
                };
            }
        }

        Rooms = currentList;
        ApplyFilter();
        NotifyChanged();
    }

    public async Task RefreshRoomStatusesAsync()
    {
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
                    UnreadCount = fresh.UnreadCount,
                    LastMessageAt = fresh.LastMessageAt,
                    LastMessagePreview = fresh.LastMessagePreview,
                    LastMessageId = fresh.LastMessageId,
                    LastMessageSenderId = fresh.LastMessageSenderId,
                    LastMessageStatus = fresh.LastMessageStatus
                };
            }
        }

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
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
            LastMessageStatus = dto.LastMessageStatus is null ? null : (MessageStatus?)(int)dto.LastMessageStatus.Value,
            MemberNames = dto.MemberNames ?? new()
        };

        if (idx >= 0)
            list[idx] = model;
        else
            list.Insert(0, model);

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }

    private void OnMemberAddedRealtime(Guid roomId, Guid userId, string displayName)
        => _toasts.Success("Member added", $"{displayName} joined");

    private void OnMessageReceived(MessageModel msg)
    {
        if (_flags.ActiveRoomId == msg.RoomId) return;
        if (_flags.GetMuted(msg.RoomId)) return;

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
            LastMessageId = r.LastMessageId,
            LastMessageSenderId = r.LastMessageSenderId,
            LastMessageStatus = r.LastMessageStatus
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }

    private void OnActiveRoomChanged(Guid? roomId)
    {
        if (roomId is null) return;
        _flags.SetUnread(roomId.Value, 0);
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
            LastMessageId = r.LastMessageId,
            LastMessageSenderId = r.LastMessageSenderId,
            LastMessageStatus = r.LastMessageStatus
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }

    private async void OnRoomUpdated(RoomUpdatedModel upd)
    {
        if (_flags.GetBlockedByMe(upd.SenderId)) return;
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == upd.RoomId);
        if (idx < 0) return;

        var r = list[idx];

        bool isActuallyNewMessage = upd.MessageId != Guid.Empty &&
                                    (!r.LastMessageId.HasValue || upd.MessageId != r.LastMessageId.Value);

        var isActive = _flags.ActiveRoomId == upd.RoomId;
        var currentUnread = _flags.GetUnread(upd.RoomId);

        // ✅ مهم: لو الغرفة نشطة (مفتوحة)، العداد = 0
        int nextUnread;
        if (isActive)
        {
            nextUnread = 0;
        }
        else
        {
            // لو مش نشطة، استخدم الـ delta
            nextUnread = upd.UnreadDelta < 0 ? 0 : currentUnread + upd.UnreadDelta;
            nextUnread = Math.Max(0, nextUnread);
        }

        _flags.SetUnread(upd.RoomId, nextUnread);

        // ✅ تحديد حالة آخر رسالة
        MessageStatus? lastMessageStatus = r.LastMessageStatus;

        // لو دي رسالة جديدة
        if (isActuallyNewMessage)
        {
            // الرسالة الجديدة تبدأ بـ Sent
            lastMessageStatus = MessageStatus.Sent;
            // خزنها في الكاش
            _lastMessageStatusCache[upd.RoomId] = (upd.MessageId, MessageStatus.Sent);
        }
        else if (_lastMessageStatusCache.TryGetValue(upd.RoomId, out var cached) && cached.messageId == r.LastMessageId)
        {
            // استخدم القيمة المخزنة في الكاش
            lastMessageStatus = cached.status;
        }

        var updatedRoom = new RoomListItemModel
        {
            Id = r.Id,
            Name = r.Name,
            Type = r.Type,
            OtherUserId = r.OtherUserId,
            OtherDisplayName = r.OtherDisplayName,
            IsMuted = r.IsMuted,
            UnreadCount = nextUnread,
            LastMessageAt = isActuallyNewMessage
                ? (upd.CreatedAt != DateTime.MinValue ? upd.CreatedAt : r.LastMessageAt)
                : r.LastMessageAt,
            LastMessagePreview = isActuallyNewMessage
                ? (!string.IsNullOrEmpty(upd.Preview) ? upd.Preview : r.LastMessagePreview)
                : r.LastMessagePreview,
            LastMessageId = isActuallyNewMessage
                ? (upd.MessageId != Guid.Empty ? upd.MessageId : (r.LastMessageId ?? Guid.Empty))
                : (r.LastMessageId ?? Guid.Empty),
            LastMessageSenderId = isActuallyNewMessage
                ? (upd.SenderId != Guid.Empty ? upd.SenderId : (r.LastMessageSenderId ?? Guid.Empty))
                : (r.LastMessageSenderId ?? Guid.Empty),
            LastMessageStatus = lastMessageStatus
        };

        if (isActuallyNewMessage)
        {
            list.RemoveAt(idx);
            list.Insert(0, updatedRoom);
        }
        else
        {
            list[idx] = updatedRoom;
        }

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

    public void MarkRoomAsReadLocal(Guid roomId, Guid? lastMessageId = null)
    {
        _flags.SetUnread(roomId, 0);

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
            LastMessageId = lastMessageId ?? r.LastMessageId,
            LastMessageSenderId = r.LastMessageSenderId,
            LastMessageStatus = r.LastMessageStatus
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }
    public void UpdateMessageStatusFromEvent(Guid messageId, Guid roomId, int total, int delivered, int read)
    {
        Console.WriteLine($"[RoomsVM] 🔔 UpdateMessageStatusFromEvent called: msg={messageId}, room={roomId}");

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0)
        {
            Console.WriteLine($"[RoomsVM] Room {roomId} not found");
            return;
        }

        var room = list[idx];

        if (room.LastMessageId == messageId)
        {
            var newStatus = (read >= total && total > 0) ? MessageStatus.Read :
                    (delivered >= 1) ? MessageStatus.Delivered :
                    MessageStatus.Sent;
            // ✅ منع التحديث للخلف
            if (room.LastMessageStatus.HasValue && newStatus < room.LastMessageStatus.Value)
            {
                Console.WriteLine($"[RoomsVM] Ignoring backward status update: {room.LastMessageStatus} -> {newStatus}");
                return;
            }

            if (room.LastMessageStatus != newStatus)
            {
                Console.WriteLine($"[RoomsVM] Updating room {roomId} status: {room.LastMessageStatus} -> {newStatus}");

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
                    LastMessageStatus = newStatus
                };

                Rooms = list;
                ApplyFilter();
                NotifyChanged();
            }
        }
    }
    // في RoomsViewModel.cs - أضف هذه الدوال

    private void OnTypingStarted(Guid roomId, Guid userId)
    {
        Console.WriteLine($"[RoomsVM] ✍️ TypingStarted for room {roomId}, user {userId}");

        lock (_typingStatus)
        {
            _typingStatus[roomId] = true;
        }

        // تحديث الغرفة المحددة فقط
        UpdateRoomTypingStatus(roomId, true);

    }
   
    private void OnTypingStopped(Guid roomId, Guid userId)
    {
        Console.WriteLine($"[RoomsVM] ✋ TypingStopped for room {roomId}, user {userId}");

        lock (_typingStatus)
        {
            _typingStatus[roomId] = false;
        }

        // تحديث الغرفة المحددة فقط
        UpdateRoomTypingStatus(roomId, false);

    }

    private void UpdateRoomTypingStatus(Guid roomId, bool isTyping)
    {
        Console.WriteLine($"[RoomsVM] 🔄 UpdateRoomTypingStatus: room={roomId}, isTyping={isTyping}");

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);

        if (idx >= 0)
        {
            var room = list[idx];
            Console.WriteLine($"[RoomsVM] Found room {room.Name}, current IsTyping={room.IsTyping}");

            if (room.IsTyping != isTyping)
            {
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
                    LastMessageStatus = room.LastMessageStatus,
                    IsTyping = isTyping
                };

                Rooms = list;
                ApplyFilter();
                NotifyChanged();
                Console.WriteLine($"[RoomsVM] ✅ Updated room {room.Name} IsTyping to {isTyping}");
            }
            else
            {
                Console.WriteLine($"[RoomsVM] No change needed for {room.Name}");
            }
        }
        else
        {
            Console.WriteLine($"[RoomsVM] Room {roomId} not found in list");
        }
    }    // دالة مساعدة لجلب حالة الـ Typing لغرفة معينة
    public bool IsRoomTyping(Guid roomId)
    {
        lock (_typingStatus)
        {
            return _typingStatus.TryGetValue(roomId, out var isTyping) && isTyping;
        }
    }

    // دالة لتحديث أو إضافة غرفة في القائمة
}