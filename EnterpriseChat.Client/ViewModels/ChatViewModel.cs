using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Chat;
using EnterpriseChat.Client.Services.Http;
using EnterpriseChat.Client.Services.Realtime;
using EnterpriseChat.Client.Services.Rooms;
using EnterpriseChat.Client.Services.Ui;
using EnterpriseChat.Domain.ValueObjects;


namespace EnterpriseChat.Client.ViewModels;

public sealed class ChatViewModel
{
    private readonly IChatService _chatService;
    private readonly IRoomService _roomService;
    private readonly IChatRealtimeClient _realtime;
    private readonly ICurrentUser _currentUser;
    private readonly ToastService _toasts;
    private readonly RoomFlagsStore _flags;
    private readonly ModerationApi _mod;


    private DateTime _lastTyping;
    private Guid? _currentRoomId;
    private bool _eventsRegistered;
    private Guid? _eventsRoomId;
    private readonly object _notifyLock = new();
    private bool _notifyQueued;
    private DateTime _lastNotifyAt = DateTime.MinValue;
    private readonly TimeSpan _notifyMinInterval = TimeSpan.FromMilliseconds(80); // 50-120 مناسب
    public event Action? Changed;
    private void NotifyChanged([System.Runtime.CompilerServices.CallerMemberName] string from = "?")
    {
        DebugChanged(from);
        Changed?.Invoke();
    }

    public ChatViewModel(
        IChatService chatService,
        IRoomService roomService,
        IChatRealtimeClient realtime,
        ICurrentUser currentUser,
        ToastService toasts,
        RoomFlagsStore flags,
        ModerationApi mod)
    {
        _chatService = chatService;
        _roomService = roomService;
        _realtime = realtime;
        _currentUser = currentUser;
        _toasts = toasts;
        _flags = flags;
        _mod = mod;

    }

    public RoomModel? Room { get; private set; }
    public GroupMembersModel? GroupMembers { get; private set; }
    public UserModel? OtherUser { get; private set; }

    public List<MessageModel> Messages { get; } = new();
    public List<UserModel> OnlineUsers { get; } = new();
    public List<UserModel> TypingUsers { get; } = new();

    // fatal only
    public string? UiError { get; private set; }

    public Guid CurrentUserId { get; private set; }

    public bool IsMuted { get; private set; }
    public bool IsBlocked { get; private set; }
    public bool IsDisconnected { get; private set; }
    public bool IsOtherDeleted { get; private set; }
    public bool IsRemoved { get; private set; }

    private int _changedCount;
    private DateTime _lastLog = DateTime.UtcNow;

    private void DebugChanged(string from)
    {
        _changedCount++;
        var now = DateTime.UtcNow;
        if ((now - _lastLog).TotalSeconds >= 2)
        {
            Console.WriteLine($"[VM Changed] last2s={_changedCount} from={from} msgs={Messages.Count} typing={TypingUsers.Count} online={OnlineUsers.Count} disconnected={IsDisconnected}");
            _changedCount = 0;
            _lastLog = now;
        }
    }

    public async Task InitializeAsync(Guid roomId)
    {
        // ✅ افصل القديم + unsubscribe من الستور
        UnregisterRealtimeEvents();
        _flags.RoomMuteChanged -= OnRoomMuteChanged;
        _flags.UserBlockChanged -= OnUserBlockChanged;

        TypingUsers.Clear();
        OnlineUsers.Clear();
        Messages.Clear();

        IsRemoved = false;
        IsOtherDeleted = false;
        UiError = null;

        // ✅ خلي قيم الحالة دايمًا تتظبط من الستور أولاً
        _currentRoomId = roomId;
        _flags.SetActiveRoom(roomId);
        IsMuted = _flags.GetMuted(roomId);
        IsBlocked = false;

        NotifyChanged();

        try
        {
            CurrentUserId = await _currentUser.GetUserIdAsync()
                ?? throw new InvalidOperationException("User not authenticated");

            Room = await _roomService.GetRoomAsync(roomId);
            if (Room == null)
            {
                UiError = "This room no longer exists.";
                NotifyChanged();
                return;
            }

            if (Room.Type == "Private" && OtherUser != null)
            {
                try
                {
                    var blocked = await _mod.GetBlockedAsync();
                    _flags.SetBlockedUsers(blocked.Select(b => b.UserId));
                    IsBlocked = _flags.GetBlocked(OtherUser.Id);
                }
                catch { }
            }


            // ✅ messages
            Messages.AddRange(await _chatService.GetMessagesAsync(roomId));
            var myId = CurrentUserId;
            var toDeliver = Messages
                .Where(m => m.SenderId != myId && m.Status < MessageStatus.Delivered)
                .Select(m => m.Id)
                .ToList();

            if (toDeliver.Any())
            {
                // fire-and-forget كلها
                _ = Task.Run(async () =>
                {
                    foreach (var id in toDeliver)
                    {
                        try { await _chatService.MarkMessageDeliveredAsync(id); }
                        catch { /* silent */ }
                    }
                });
            }
            NotifyChanged();

            // ✅ group members / other user
            if (Room.Type == "Group")
            {
                var dto = await _chatService.GetGroupMembersAsync(roomId);
                GroupMembers = new GroupMembersModel
                {
                    OwnerId = dto.OwnerId,
                    Members = dto.Members.Select(m => new UserModel
                    {
                        Id = m.Id,
                        DisplayName = m.DisplayName
                    }).ToList()
                };
            }
            else
            {
                GroupMembers = null;
            }

            if (Room.Type == "Private")
            {
                if (Room.OtherUserId == null)
                {
                    IsOtherDeleted = true;
                    OtherUser = null;
                }
                else
                {
                    OtherUser = new UserModel
                    {
                        Id = Room.OtherUserId.Value,
                        DisplayName = Room.OtherDisplayName ?? "User",
                        IsOnline = false
                    };

                    // ✅ sync block مع الستور
                    IsBlocked = _flags.GetBlocked(OtherUser.Id);

                }
            }
            else
            {
                OtherUser = null;
            }

            // ✅ subscribe للستور بعد ما عرفنا room + other user
            _flags.RoomMuteChanged += OnRoomMuteChanged;
            _flags.UserBlockChanged += OnUserBlockChanged;

            RegisterRealtimeEvents(roomId);

            // ✅ realtime connect + join
            await _realtime.ConnectAsync();
            await _realtime.JoinRoomAsync(roomId);

            // ✅ صفّر unread فور الدخول (مرة واحدة) + بعد Join
            var lastMsg = Messages
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            if (lastMsg != null)
            {
                try
                {
                    await MarkRoomReadAsync(roomId, lastMsg.Id);
                }
                catch { }
            }

            // ✅ صفر محليًا فورًا (القائمة)
            _flags.SetUnread(roomId, 0);


            if (Room?.Type == "Private")
            {
                try
                {
                    var blocked = await _mod.GetBlockedAsync();
                    _flags.SetBlockedUsers(blocked.Select(b => b.UserId));
                }
                catch { /* ignore preload errors */ }
            }

            // ✅ snapshot presence للـ private chat (بدون refresh)
            if (Room?.Type == "Private" && OtherUser is not null)
            {
                var set = _realtime.State.OnlineUsers?.ToHashSet() ?? new HashSet<Guid>();
                OtherUser.IsOnline = set.Contains(OtherUser.Id);

                if (OtherUser.IsOnline)
                    OtherUser.LastSeen = null;

                NotifyChanged();
            }

            if (Room.Type == "Group" && GroupMembers is not null)
                RebuildPresenceFromRealtime();

            NotifyChanged();
        }
        catch (Exception ex)
        {
            UiError = ex.Message;
            Room = null;
            NotifyChanged();
        }
    }



    private void RegisterRealtimeEvents(Guid roomId)
    {
        if (_eventsRegistered && _eventsRoomId == roomId)
            return;

        UnregisterRealtimeEvents();
        _eventsRoomId = roomId;

        _realtime.MessageReceived += OnMessageReceived;
        _realtime.MessageDelivered += OnMessageDelivered;
        _realtime.MessageRead += OnMessageRead;
        _realtime.RoomMuteChanged += OnRoomMuteChanged;

        _realtime.UserOnline += OnUserOnline;
        _realtime.UserOffline += OnUserOffline;
        _realtime.RoomPresenceUpdated += OnRoomPresenceUpdated;

        _realtime.TypingStarted += OnTypingStarted;
        _realtime.TypingStopped += OnTypingStopped;

        _realtime.Disconnected += OnDisconnected;
        _realtime.Reconnected += OnReconnected;
        _realtime.RemovedFromRoom += OnRemovedFromRoom;

        _eventsRegistered = true;
    }

    private void UnregisterRealtimeEvents()
    {
        if (!_eventsRegistered) return;

        _realtime.MessageReceived -= OnMessageReceived;
        _realtime.MessageDelivered -= OnMessageDelivered;
        _realtime.MessageRead -= OnMessageRead;

        _realtime.UserOnline -= OnUserOnline;
        _realtime.UserOffline -= OnUserOffline;
        _realtime.RoomPresenceUpdated -= OnRoomPresenceUpdated;

        _realtime.TypingStarted -= OnTypingStarted;
        _realtime.TypingStopped -= OnTypingStopped;

        _realtime.Disconnected -= OnDisconnected;
        _realtime.Reconnected -= OnReconnected;
        _realtime.RemovedFromRoom -= OnRemovedFromRoom;
        _realtime.RoomMuteChanged -= OnRoomMuteChanged;

        _eventsRegistered = false;
        _eventsRoomId = null;
    }

    private void OnRoomMuteChanged(Guid rid, bool muted)
    {
        if (_eventsRoomId != rid) return;

        IsMuted = muted;
        NotifyChanged();
    }


    private void RebuildPresenceFromRealtime()
    {
        if (GroupMembers is null) return;

        var onlineSet = _realtime.State.OnlineUsers.ToHashSet();

        OnlineUsers.Clear();
        OnlineUsers.AddRange(
            GroupMembers.Members
                .Where(m => m.Id != CurrentUserId)
                .Where(m => onlineSet.Contains(m.Id))
                .Select(m => new UserModel
                {
                    Id = m.Id,
                    DisplayName = m.DisplayName,
                    IsOnline = true
                })
        );

        TypingUsers.RemoveAll(u => !onlineSet.Contains(u.Id));
        NotifyChanged();
    }

    public IReadOnlyList<UserModel> GetAllMembersForDrawer()
    {
        if (GroupMembers is null) return Array.Empty<UserModel>();

        var onlineSet = _realtime.State.OnlineUsers.ToHashSet();

        return GroupMembers.Members
            .Select(m => new UserModel
            {
                Id = m.Id,
                DisplayName = m.DisplayName,
                IsOnline = onlineSet.Contains(m.Id)
            })
            .OrderByDescending(u => u.IsOnline)
            .ThenBy(u => u.DisplayName)
            .ToList();
    }

    private UserModel? FindMember(Guid userId)
        => GroupMembers?.Members
            .Where(m => m.Id == userId)
            .Select(m => new UserModel { Id = m.Id, DisplayName = m.DisplayName })
            .FirstOrDefault();

    public async Task SendAsync(Guid roomId, string text)
    {
        if (IsMuted)
        {
            _toasts.Warning("Muted", "This chat is muted. Unmute to send messages.");
            return;
        }


        if (IsBlocked)
        {
            _toasts.Warning("Blocked", "You can't send messages to this user.");
            return;
        }

        if (IsOtherDeleted)
        {
            _toasts.Warning("Unavailable", "This user is no longer available.");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Length > 2000)
        {
            _toasts.Warning("Too long", "Message is too long.");
            return;
        }

        // ✅ مهم: temp id unique (مش Guid.Empty)
        var tempId = Guid.NewGuid();

        var pending = new MessageModel
        {
            Id = tempId,
            RoomId = roomId,
            SenderId = CurrentUserId,
            Content = text,
            CreatedAt = DateTime.UtcNow,
            Status = MessageStatus.Pending
        };

        Messages.Add(pending);
        NotifyChanged();

        try
        {
            var dto = await _chatService.SendMessageAsync(roomId, text);
            if (dto != null)
            {
                pending.Id = dto.Id;
                pending.Status = MessageStatus.Sent;
                pending.Error = null;
            }
            NotifyChanged();
        }
        catch
        {
            pending.Status = MessageStatus.Failed;
            pending.Error = "Failed to send message.";
            _toasts.Error("Send failed", "Network error. Tap retry.");
            NotifyChanged();
        }
    }

    private void NotifyChangedThrottled()
    {
        lock (_notifyLock)
        {
            // لو فيه Update متسجل بالفعل، ما نكررش
            if (_notifyQueued) return;

            var now = DateTime.UtcNow;
            var elapsed = now - _lastNotifyAt;

            if (elapsed >= _notifyMinInterval)
            {
                _lastNotifyAt = now;
                Changed?.Invoke();
                return;
            }

            _notifyQueued = true;
            var delay = _notifyMinInterval - elapsed;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay);
                    lock (_notifyLock)
                    {
                        _notifyQueued = false;
                        _lastNotifyAt = DateTime.UtcNow;
                    }
                    Changed?.Invoke();
                }
                catch { }
            });
        }
    }


    public async Task NotifyTypingAsync(Guid roomId)
    {
        if ((DateTime.UtcNow - _lastTyping).TotalMilliseconds < 800)
            return;

        _lastTyping = DateTime.UtcNow;

        try
        {
            await _realtime.NotifyTypingAsync(roomId);
        }
        catch
        {
            // toast optional – typing مش critical
        }
    }

    public async Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId)
    {
        try
        {
            await _realtime.MarkRoomReadAsync(roomId, lastMessageId);
        }
        catch
        {
            try { await _chatService.MarkRoomReadAsync(roomId, lastMessageId); }
            catch { }
        }
    }

    public async Task RemoveMemberAsync(Guid roomId, Guid userId)
    {
        try
        {
            await _chatService.RemoveMemberAsync(roomId, userId);
            GroupMembers?.Members.RemoveAll(u => u.Id == userId);
            RebuildPresenceFromRealtime();
            _toasts.Success("Done", "Member removed.");
        }
        catch
        {
            _toasts.Error("Failed", "Could not remove member.");
        }
    }

    public async Task BlockUserAsync(Guid userId)
    {
        try
        {
            await _chatService.BlockUserAsync(userId);
            IsBlocked = true;
            _flags.SetBlocked(userId, true); // ✅
            TypingUsers.Clear();
            NotifyChanged();
        }
        catch
        {
            _toasts.Error("Failed", "Could not block user.");
        }
    }

    public async Task UnblockUserAsync(Guid userId)
    {
        try
        {
            await _mod.UnblockAsync(userId);
            IsBlocked = false;
            _flags.SetBlocked(userId, false);
            NotifyChanged();
        }
        catch
        {
            _toasts.Error("Failed", "Could not unblock user.");
        }
    }


    public async Task ToggleMuteAsync(Guid roomId)
    {
        try
        {
            // ✅ خلي الستوري هو مصدر الحقيقة
            var currentlyMuted = _flags.GetMuted(roomId);
            var nextMuted = !currentlyMuted;

            if (nextMuted)
                await _chatService.MuteAsync(roomId);
            else
                await _chatService.UnmuteAsync(roomId);

            // ✅ تحديث فوري لكل الصفحات
            _flags.SetMuted(roomId, nextMuted);

            // ✅ تحديث فوري لزرار TopBar في نفس الشات
            IsMuted = nextMuted;

            NotifyChanged();
        }
        catch
        {
            _toasts.Error("Failed", "Could not toggle mute.");
        }
    }


    public async Task RefreshRoomStateAsync(Guid roomId)
    {
        try
        {
            var room = await _roomService.GetRoomAsync(roomId);
            if (room != null)
            {
                Room = room;
                IsMuted = room.IsMuted;
                NotifyChanged();
            }
        }
        catch { }
    }

    public async Task DisposeAsync()
    {
        _flags.SetActiveRoom(null);

        // ✅ unsubscribe من الستور
        _flags.RoomMuteChanged -= OnRoomMuteChanged;
        _flags.UserBlockChanged -= OnUserBlockChanged;

        UnregisterRealtimeEvents();

        if (_currentRoomId != null)
            await _realtime.LeaveRoomAsync(_currentRoomId.Value);

        await _realtime.DisconnectAsync();

        _currentRoomId = null;
    }

    private void OnRoomMuteChanged(Guid roomId)
    {
        if (_currentRoomId != roomId) return;
        IsMuted = _flags.GetMuted(roomId);
        NotifyChanged();
    }

    private void OnUserBlockChanged(Guid userId)
    {
        if (OtherUser?.Id != userId) return;
        IsBlocked = _flags.GetBlocked(userId);
        NotifyChanged();
    }

    // ===== Realtime handlers =====
    private void OnMessageReceived(MessageModel message)
    {
        var existing = Messages.FirstOrDefault(m =>
            m.Status == MessageStatus.Pending &&
            m.Content == message.Content &&
            m.SenderId == message.SenderId);

        if (existing != null)
        {
            existing.Id = message.Id;
            existing.Status = MessageStatus.Sent;
            existing.Error = null;
        }
        else
        {
            Messages.Add(message);
        }

        NotifyChanged();
        // ✅ لو الرسالة جاية من شخص تاني وأنا فاتح نفس الروم:
        if (_currentRoomId == message.RoomId && message.SenderId != CurrentUserId)
        {
            if (message.Status < MessageStatus.Delivered)
            {
                _ = Task.Run(async () =>
                {
                    try { await _chatService.MarkMessageDeliveredAsync(message.Id); }
                    catch { }
                });
            }
        }
    }

    private void OnMessageDelivered(Guid messageId)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg?.Status == MessageStatus.Sent)
            msg.Status = MessageStatus.Delivered;

        NotifyChangedThrottled();
    }

    public async Task RefreshMuteStateAsync(Guid roomId)
    {
        try
        {
            var room = await _roomService.GetRoomAsync(roomId);
            if (room != null)
            {
                Room = room;

                // ✅ عدّل اسم الخاصية حسب الموديل عندك:
                // لو عندك room.IsMuted استخدمها، لو اسمها Muted أو IsRoomMuted إلخ
                IsMuted = room.IsMuted;

                NotifyChanged();
            }
        }
        catch
        {
            // ignore
        }
    }


    private void OnMessageRead(Guid id)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == id);
        if (msg != null && msg.Status != MessageStatus.Read)
            msg.Status = MessageStatus.Read;

        NotifyChangedThrottled();
    }


    private void OnUserOnline(Guid id)
    {
        if (OtherUser?.Id == id)
        {
            OtherUser.IsOnline = true;
            OtherUser.LastSeen = null;
        }

        if (Room?.Type == "Group")
            RebuildPresenceFromRealtime();

        NotifyChangedThrottled();
    }


    private void OnUserOffline(Guid id)
    {
        if (OtherUser?.Id == id)
        {
            OtherUser.IsOnline = false;
            OtherUser.LastSeen = DateTime.UtcNow;
        }

        if (Room?.Type == "Group")
            RebuildPresenceFromRealtime();

        NotifyChangedThrottled();
    }


    private void OnRoomPresenceUpdated(Guid rid, int _)
    {
        if (_eventsRoomId != rid) return;
        RebuildPresenceFromRealtime();
    }

    private void OnTypingStarted(Guid rid, Guid uid)
    {
        if (_eventsRoomId != rid || uid == CurrentUserId)
            return;

        if (TypingUsers.Any(u => u.Id == uid))
            return;

        var user = OnlineUsers.FirstOrDefault(u => u.Id == uid) ?? FindMember(uid);
        if (user != null)
        {
            user.IsOnline = true;
            TypingUsers.Add(user);
            NotifyChanged();
        }
    }

    private void OnTypingStopped(Guid rid, Guid uid)
    {
        if (_eventsRoomId != rid) return;

        TypingUsers.RemoveAll(u => u.Id == uid);
        NotifyChanged();
    }

    private void OnDisconnected()
    {
        IsDisconnected = true;
        NotifyChanged();
    }

    private void OnReconnected()
    {
        IsDisconnected = false;
        NotifyChanged();

        if (_eventsRoomId is Guid rid)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _realtime.JoinRoomAsync(rid);
                    RebuildPresenceFromRealtime();
                }
                catch { }
            });
        }
    }

    private void OnRemovedFromRoom(Guid rid)
    {
        if (_eventsRoomId == rid)
        {
            IsRemoved = true;
            NotifyChanged();
        }
    }
}