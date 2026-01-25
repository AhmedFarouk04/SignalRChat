using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Chat;
using EnterpriseChat.Client.Services.Realtime;
using EnterpriseChat.Client.Services.Rooms;
using EnterpriseChat.Client.Services.Ui;

namespace EnterpriseChat.Client.ViewModels;

public sealed class ChatViewModel
{
    private readonly IChatService _chatService;
    private readonly IRoomService _roomService;
    private readonly IChatRealtimeClient _realtime;
    private readonly ICurrentUser _currentUser;
    private readonly ToastService _toasts;

    private DateTime _lastTyping;
    private Guid? _currentRoomId;
    private bool _eventsRegistered;
    private Guid? _eventsRoomId;

    public event Action? Changed;
    private void NotifyChanged() => Changed?.Invoke();

    public ChatViewModel(
        IChatService chatService,
        IRoomService roomService,
        IChatRealtimeClient realtime,
        ICurrentUser currentUser,
        ToastService toasts)
    {
        _chatService = chatService;
        _roomService = roomService;
        _realtime = realtime;
        _currentUser = currentUser;
        _toasts = toasts;
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

    public async Task InitializeAsync(Guid roomId)
    {
        UnregisterRealtimeEvents();

        TypingUsers.Clear();
        OnlineUsers.Clear();
        Messages.Clear();

        IsRemoved = false;
        IsOtherDeleted = false;
        UiError = null;

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

            _currentRoomId = roomId;

            Messages.AddRange(await _chatService.GetMessagesAsync(roomId));
            NotifyChanged();

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
                }
            }
            else
            {
                OtherUser = null;
            }

            RegisterRealtimeEvents(roomId);

            await _realtime.ConnectAsync();
            await _realtime.JoinRoomAsync(roomId);

            if (Room.Type == "Group" && GroupMembers is not null)
                RebuildPresenceFromRealtime();

            NotifyChanged();
        }
        catch
        {
            UiError = "Failed to load chat. Please try again.";
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

        _eventsRegistered = false;
        _eventsRoomId = null;
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
            TypingUsers.Clear();
            _toasts.Success("Blocked", "User blocked.");
            NotifyChanged();
        }
        catch
        {
            _toasts.Error("Failed", "Could not block user.");
        }
    }

    public async Task ToggleMuteAsync(Guid roomId)
    {
        try
        {
            if (!IsMuted) await _chatService.MuteAsync(roomId);
            else await _chatService.UnmuteAsync(roomId);

            IsMuted = !IsMuted;
            _toasts.Success("Updated", IsMuted ? "Muted." : "Unmuted.");
            NotifyChanged();
        }
        catch
        {
            _toasts.Error("Failed", "Could not toggle mute.");
        }
    }

    public async Task DisposeAsync()
    {
        UnregisterRealtimeEvents();

        if (_currentRoomId != null)
            await _realtime.LeaveRoomAsync(_currentRoomId.Value);

        await _realtime.DisconnectAsync();
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
    }

    private void OnMessageDelivered(Guid messageId)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg?.Status == MessageStatus.Sent)
            msg.Status = MessageStatus.Delivered;

        NotifyChanged();
    }

    private void OnMessageRead(Guid id)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == id);
        if (msg != null && msg.Status != MessageStatus.Read)
            msg.Status = MessageStatus.Read;

        NotifyChanged();
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

        NotifyChanged();
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

        NotifyChanged();
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
