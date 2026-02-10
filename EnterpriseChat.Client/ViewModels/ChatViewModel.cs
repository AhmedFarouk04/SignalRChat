using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Chat;
using EnterpriseChat.Client.Services.Http;
using EnterpriseChat.Client.Services.Reaction;
using EnterpriseChat.Client.Services.Realtime;
using EnterpriseChat.Client.Services.Rooms;
using EnterpriseChat.Client.Services.Ui;
using EnterpriseChat.Domain.Enums;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EnterpriseChat.Client.ViewModels;

// ✅ استخدام ClientMessageStatus بشكل صحيح
using ClientMessageStatus = EnterpriseChat.Client.Models.MessageStatus;

public sealed class ChatViewModel : INotifyPropertyChanged, IAsyncDisposable
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
    private readonly TimeSpan _notifyMinInterval = TimeSpan.FromMilliseconds(80);
    private DateTime _lastTypingSent = DateTime.MinValue;
    private readonly TimeSpan _typingThrottle = TimeSpan.FromMilliseconds(800);
    private readonly ReactionsApi _reactionsApi;

    // ✅ الأحداث - تعريف واحد فقط لكل حدث
    public event Action? Changed;
    public event Func<MessageModel, Task>? MessageReceived;
    public event Func<MessageModel, Task>? MessageUpdated;
    public event Func<MessageModel, Task>? MessageReplyReceived;
    public event Action<ReplyContext?>? ReplyContextChanged;

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool _isPinModalOpen;
    public bool IsPinModalOpen { get => _isPinModalOpen; set { _isPinModalOpen = value; NotifyChanged(); } }
    private Guid? _messageIdToPin;
    public bool IsMessagePinned(Guid messageId) => PinnedMessages.Any(m => m.Id == messageId);
    public ObservableCollection<MessageModel> PinnedMessages { get; } = new();

    private bool _isSelectionMode;
    public bool IsSelectionMode { get => _isSelectionMode; set { _isSelectionMode = value; if (!value) SelectedMessageIds.Clear(); NotifyChanged(); } }

    public ObservableCollection<Guid> SelectedMessageIds { get; } = new();

    public Guid? PinnedMessageId => PinnedMessages.LastOrDefault()?.Id;
    public MessageModel? PinnedMessage => PinnedMessages.LastOrDefault();
    public void OpenPinModal(Guid messageId)
    {
        if (IsMessagePinned(messageId))
        {
            // إذا كانت مثبتة، نفذ ميثود الـ Unpin اللي هنعدلها تحت
            _ = UnpinMessageAsync(messageId);
            return;
        }
        _messageIdToPin = messageId;
        IsPinModalOpen = true;
        NotifyChanged(); // تأكد إن الميثود دي بتنادي StateHasChanged في الـ UI
    }
    // أضف هذه الخصائص في ChatViewModel
    public void ToggleMessageSelection(Guid messageId)
    {
        if (SelectedMessageIds.Contains(messageId))
            SelectedMessageIds.Remove(messageId);
        else
            SelectedMessageIds.Add(messageId);
        NotifyChanged();
    }

    // ميثود لفتح الـ Forward Modal (هنحتاجها لاحقاً)
    public bool IsForwardModalOpen { get; set; }
    public void OpenForwardModal() => IsForwardModalOpen = true;
    public async Task ConfirmPinAsync(string duration)
    {
        if (!_messageIdToPin.HasValue) return;
        var msgId = _messageIdToPin.Value;
        IsPinModalOpen = false;

        try
        {
            await _chatService.PinMessageAsync(Room!.Id, msgId, duration);

            // إضافة رسالة نظام
            Messages.Add(new MessageModel
            {
                Id = Guid.NewGuid(),
                Content = "You pinned a message",
                Type = "System",
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            });

            var msg = Messages.FirstOrDefault(m => m.Id == msgId);
            if (msg != null)
            {
                // التحقق إذا كانت الرسالة موجودة أصلاً (عشان التكرار)
                if (!PinnedMessages.Any(x => x.Id == msgId))
                {
                    // إذا وصلنا لـ 3 رسائل، نشيل أقدم واحدة (أول واحدة في القائمة)
                    if (PinnedMessages.Count >= 3)
                    {
                        PinnedMessages.RemoveAt(0);
                    }
                    PinnedMessages.Add(msg);
                }
            }

            NotifyChanged();
        }
        catch (Exception ex)
        {
            _toasts.Error("Pin failed", ex.Message);
        }
    }
    public async Task UnpinMessageAsync(Guid messageId)
    {
        // 1. التحديث المحلي (السر كله هنا)
        var msg = PinnedMessages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            PinnedMessages.Remove(msg);
            NotifyChanged(); // الـ Topbar هيحس فوراً إن الـ Count نقص أو القائمة فضيت
        }

        try
        {
            // 2. هنا حط الكود بتاعك اللي بيكلم السيرفر 
            // مثلاً: await _hubConnection.InvokeAsync("UnpinMessage", messageId);
            // أو: await YourActualServiceName.UnpinAsync(messageId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error unpinning: {ex.Message}");
        }
    }
    // EnterpriseChat.Client/ViewModels/ChatViewModel.cs

    public async Task<bool> ExecuteForwardAsync(List<Guid> targetRoomIds)
    {
        if (!SelectedMessageIds.Any() || !targetRoomIds.Any()) return false;

        try
        {
            // 1. تجهيز الطلب
            var request = new ForwardMessagesRequest
            {
                MessageIds = SelectedMessageIds.ToList(),
                TargetRoomIds = targetRoomIds
            };

            // 2. نداء الـ API (تأكد إنك ضفت EndPoint في الـ ChatService)
            await _chatService.ForwardMessagesAsync(request);

            // 3. إنهاء وضع الاختيار وتقديم تغذية راجعة
            IsSelectionMode = false;
            _toasts.Success("Success", "Messages forwarded successfully!");

            NotifyChanged();
            return true;
        }
        catch (Exception ex)
        {
            _toasts.Error("Forward failed", ex.Message);
            return false;
        }
    }
    public async Task PinMessageAsync(Guid? messageId, string? duration = null)
    {
        try
        {
            await _chatService.PinMessageAsync(Room!.Id, messageId, duration);

            // تحديث القائمة محلياً Real-time
            if (messageId == null)
            {
                PinnedMessages.Clear(); // إلغاء الكل مؤقتاً لتبسيط المنطق
            }
            else
            {
                var msg = Messages.FirstOrDefault(m => m.Id == messageId);
                if (msg != null && !PinnedMessages.Any(m => m.Id == msg.Id))
                {
                    PinnedMessages.Add(msg);
                }
            }
            NotifyChanged();
        }
        catch (Exception ex)
        {
            _toasts.Error("Operation failed", ex.Message);
        }
    }

    private void NotifyChanged([CallerMemberName] string from = "?")
    {
        DebugChanged(from);
        Changed?.Invoke();
    }

    // ✅ Constructor
    public ChatViewModel(
        IChatService chatService,
        IRoomService roomService,
        IChatRealtimeClient realtime,
        ICurrentUser currentUser,
        ToastService toasts,
        RoomFlagsStore flags,
        ModerationApi mod, ReactionsApi reactionsApi
        )
    {
        _chatService = chatService;
        _roomService = roomService;
        _realtime = realtime;
        _currentUser = currentUser;
        _toasts = toasts;
        _flags = flags;
        _mod = mod;
        _reactionsApi = reactionsApi;

    }

    // ✅ الخصائص العامة
    public RoomModel? Room { get; private set; }
    public GroupMembersModel? GroupMembers { get; private set; }
    public UserModel? OtherUser { get; private set; }
    public ObservableCollection<MessageModel> Messages { get; } = new();
    public ObservableCollection<UserModel> OnlineUsers { get; } = new();
    public ObservableCollection<UserModel> TypingUsers { get; } = new();
    public string? UiError { get; private set; }
    public Guid CurrentUserId { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsBlocked { get; private set; }
    public bool IsDisconnected { get; private set; }
    public bool IsOtherDeleted { get; private set; }
    public bool IsRemoved { get; private set; }
    private string _searchQuery = string.Empty;
    public string SearchQuery { get => _searchQuery; set { _searchQuery = value; NotifyChanged(); _ = PerformSearchAsync(); } }

    private bool _isSearching;
    public bool IsSearching { get => _isSearching; set { _isSearching = value; if (!value) SearchResults.Clear(); NotifyChanged(); } }

    public ObservableCollection<MessageModel> SearchResults { get; } = new();

    private CancellationTokenSource? _searchCts;
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

    // ✅ InitializeAsync
    public async Task InitializeAsync(Guid roomId)
    {
        UnregisterRealtimeEvents();
        _flags.RoomMuteChanged -= OnRoomMuteChanged;
        _flags.UserBlockChanged -= OnUserBlockChanged;

        TypingUsers.Clear();
        OnlineUsers.Clear();
        Messages.Clear();

        IsRemoved = false;
        IsOtherDeleted = false;
        UiError = null;

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

            // ✅ Load group members إذا كانت group
            GroupMembersModel? groupMembers = null;
            if (Room.Type == "Group")
            {
                var dto = await _chatService.GetGroupMembersAsync(roomId);
                groupMembers = new GroupMembersModel
                {
                    OwnerId = dto.OwnerId,
                    Members = dto.Members.Select(m => new UserModel
                    {
                        Id = m.Id,
                        DisplayName = m.DisplayName ?? "User"
                    }).ToList()
                };
                GroupMembers = groupMembers;
            }
            else
            {
                GroupMembers = null;
            }

            // ✅ Private other user
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
                    IsBlocked = _flags.GetBlocked(OtherUser.Id);
                }
            }
            else
            {
                OtherUser = null;
            }

            // ✅ Load messages
            var loaded = (await _chatService.GetMessagesAsync(roomId, 0, 200)).ToList();
            loaded.Reverse();

            Messages.Clear();
            foreach (var msg in loaded)
            {
                Messages.Add(msg);
            }
            // جوه InitializeAsync بعد تحميل الرسائل
            var pinnedMsg = loaded.FirstOrDefault(m => m.Id == Room.PinnedMessageId);
            if (pinnedMsg != null)
            {
                PinnedMessages.Add(pinnedMsg);
            }
            // أو لو عندك API خاص بالـ Pinned Messages ناديه هنا

            NotifyChanged();

            // ✅ حساب TotalRecipients
            var myId = CurrentUserId;
            var memberCount = Room.Type == "Group" ? (groupMembers?.Members.Count ?? 1) - 1 : 1;

            foreach (var msg in Messages)
            {
                msg.TotalRecipients = memberCount;
            }

            // ✅ Mark delivered
            var toDeliver = Messages
                .Where(m => m.SenderId != myId && (int)m.Status < (int)ClientMessageStatus.Delivered)
                .Select(m => m.Id)
                .ToList();

            if (toDeliver.Any())
            {
                _ = Task.Run(async () =>
                {
                    foreach (var id in toDeliver)
                    {
                        try { await _chatService.MarkMessageDeliveredAsync(id); }
                        catch { }
                    }
                });
            }

            // ✅ Subscribe to events
            _flags.RoomMuteChanged += OnRoomMuteChanged;
            _flags.UserBlockChanged += OnUserBlockChanged;
            RegisterRealtimeEvents(roomId);

            await _realtime.ConnectAsync();
            await _realtime.JoinRoomAsync(roomId);

            var lastMsg = Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
            if (lastMsg != null)
            {
                try { await MarkRoomReadAsync(roomId, lastMsg.Id); }
                catch { }
            }

            _flags.SetUnread(roomId, 0);

            if (Room?.Type == "Private" && OtherUser is not null)
            {
                var set = _realtime.State.OnlineUsers?.ToHashSet() ?? new HashSet<Guid>();
                OtherUser.IsOnline = set.Contains(OtherUser.Id);
                if (OtherUser.IsOnline) OtherUser.LastSeen = null;
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

    public async Task<MessageReactionsDetailsDto?> GetMessageReactionsDetailsAsync(Guid messageId)
    {
        try
        {
            return await _chatService.GetMessageReactionsDetailsAsync(messageId);
        }
        catch
        {
            return null;
        }
    }


    // ✅ Register/Unregister Realtime Events
    private void RegisterRealtimeEvents(Guid roomId)
    {
        if (_eventsRegistered && _eventsRoomId == roomId)
            return;

        UnregisterRealtimeEvents();
        _eventsRoomId = roomId;

        _realtime.MessageReceived += OnRealtimeMessageReceived;
        _realtime.MessageDelivered += OnMessageDelivered;
        _realtime.MessageRead += OnMessageRead;
        _realtime.RoomMuteChanged += OnRealtimeRoomMuteChanged;
        _realtime.UserOnline += OnUserOnline;
        _realtime.UserOffline += OnUserOffline;
        _realtime.RoomPresenceUpdated += OnRoomPresenceUpdated;
        _realtime.TypingStarted += OnTypingStarted;
        _realtime.TypingStopped += OnTypingStopped;
        _realtime.Disconnected += OnDisconnected;
        _realtime.Reconnected += OnReconnected;
        _realtime.RemovedFromRoom += OnRemovedFromRoom;
        _realtime.GroupRenamed += OnGroupRenamed;
        _realtime.MemberAdded += OnMemberAdded;
        _realtime.MemberRemoved += OnMemberRemoved;
        _realtime.GroupDeleted += OnGroupDeleted;
        _realtime.AdminPromoted += OnMemberRoleChanged;
        _realtime.AdminDemoted += OnMemberRoleChanged;
        _realtime.OwnerTransferred += OnOwnerTransferred;
        _realtime.MessageStatusUpdated += OnMessageStatusUpdated;
        _realtime.MessageDeliveredToAll += OnMessageDeliveredToAll;
        _realtime.MessageReadToAll += OnMessageReadToAll;
        _realtime.MessageReactionUpdated += OnMessageReactionUpdated;
        _realtime.MessageUpdated += OnMessageUpdated;
        _realtime.MessageDeleted += OnMessageDeleted;
        _realtime.MessagePinned += OnMessagePinned;
        _eventsRegistered = true;
    }

    private void UnregisterRealtimeEvents()
    {
        if (!_eventsRegistered) return;

        _realtime.MessageReceived -= OnRealtimeMessageReceived;
        _realtime.MessageDelivered -= OnMessageDelivered;
        _realtime.MessageRead -= OnMessageRead;
        _realtime.RoomMuteChanged -= OnRealtimeRoomMuteChanged;
        _realtime.UserOnline -= OnUserOnline;
        _realtime.UserOffline -= OnUserOffline;
        _realtime.RoomPresenceUpdated -= OnRoomPresenceUpdated;
        _realtime.TypingStarted -= OnTypingStarted;
        _realtime.TypingStopped -= OnTypingStopped;
        _realtime.Disconnected -= OnDisconnected;
        _realtime.Reconnected -= OnReconnected;
        _realtime.RemovedFromRoom -= OnRemovedFromRoom;
        _realtime.GroupRenamed -= OnGroupRenamed;
        _realtime.MemberAdded -= OnMemberAdded;
        _realtime.MemberRemoved -= OnMemberRemoved;
        _realtime.GroupDeleted -= OnGroupDeleted;
        _realtime.AdminPromoted -= OnMemberRoleChanged;
        _realtime.AdminDemoted -= OnMemberRoleChanged;
        _realtime.OwnerTransferred -= OnOwnerTransferred;
        _realtime.MessageStatusUpdated -= OnMessageStatusUpdated;
        _realtime.MessageDeliveredToAll -= OnMessageDeliveredToAll;
        _realtime.MessageReadToAll -= OnMessageReadToAll;
        _realtime.MessageReactionUpdated -= OnMessageReactionUpdated;

        _eventsRegistered = false;
        _eventsRoomId = null;
    }

    // ✅ Realtime Event Handlers
    private void OnRealtimeMessageReceived(MessageModel message)
    {
        var existing = Messages.FirstOrDefault(m =>
             (m.Status == ClientMessageStatus.Pending || m.Status == ClientMessageStatus.Sent) &&
             m.Content == message.Content &&
             m.SenderId == message.SenderId &&
             Math.Abs((m.CreatedAt - message.CreatedAt).TotalSeconds) < 5);

        if (existing != null)
        {
            existing.Id = message.Id;
            existing.Status = ClientMessageStatus.Sent;
            existing.Error = null;
            existing.ReplyInfo = message.ReplyInfo;
            existing.ReplyToMessageId = message.ReplyToMessageId;
        }
        else
        {
            Messages.Add(message);
        }

        MessageReceived?.Invoke(message);
        NotifyChanged();
    }

    private void OnMessageDelivered(Guid messageId)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg?.Status == ClientMessageStatus.Sent)
        {
            msg.Status = ClientMessageStatus.Delivered;
            UpdateMessageStatusStats(msg);
            MessageUpdated?.Invoke(msg);
            NotifyChangedThrottled();
        }
    }

    private void OnMessageRead(Guid id)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == id);
        if (msg != null && msg.Status != ClientMessageStatus.Read)
        {
            msg.Status = ClientMessageStatus.Read;
            UpdateMessageStatusStats(msg);
            MessageUpdated?.Invoke(msg);
            NotifyChangedThrottled();
        }
    }

    private void OnRealtimeRoomMuteChanged(Guid rid, bool muted)
    {
        if (_eventsRoomId != rid) return;
        IsMuted = muted;
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
        if (_eventsRoomId != rid || uid == CurrentUserId) return;

        var existing = TypingUsers.FirstOrDefault(u => u.Id == uid);
        if (existing != null) return;

        // ابحث عن الـ user (من members أو online أو other)
        var user = FindMember(uid)
                   ?? OnlineUsers.FirstOrDefault(u => u.Id == uid)
                   ?? (OtherUser?.Id == uid ? OtherUser : null);

        if (user != null)
        {
            var typingUser = new UserModel
            {
                Id = uid,
                DisplayName = GetSenderName(uid), // استخدم الـ method اللي عندك
                IsOnline = true
            };
            TypingUsers.Add(typingUser);
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

    private async void OnGroupRenamed(Guid roomId, string newName)
    {
        if (_currentRoomId != roomId) return;
        await RefreshRoomStateAsync(roomId);
    }

    private async void OnMemberAdded(Guid roomId, Guid userId, string displayName)
    {
        if (_currentRoomId != roomId) return;
        await RefreshGroupMembersAsync();
        RebuildPresenceFromRealtime();
        _toasts.Info("Member added", $"{displayName} was added to the group");
    }

    private async void OnMemberRemoved(Guid roomId, Guid userId, string? removerName)
    {
        if (_currentRoomId != roomId) return;
        if (userId == CurrentUserId)
        {
            IsRemoved = true;
            NotifyChanged();
            return;
        }
        await RefreshGroupMembersAsync();
        RebuildPresenceFromRealtime();
        var message = removerName != null
            ? $"{removerName} removed a member"
            : "A member was removed";
        _toasts.Info("Member removed", message);
    }

    private void OnGroupDeleted(Guid roomId)
    {
        if (_currentRoomId == roomId)
        {
            UiError = "This group has been deleted.";
            NotifyChanged();
        }
    }

    private async void OnMemberRoleChanged(Guid roomId, Guid userId)
    {
        if (_currentRoomId != roomId) return;
        await RefreshGroupMembersAsync();
    }

    private async void OnOwnerTransferred(Guid roomId, Guid newOwnerId)
    {
        if (_currentRoomId != roomId) return;
        await RefreshGroupMembersAsync();
    }

    private void OnMessageStatusUpdated(Guid messageId, Guid userId, int status)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            UpdateMessageStatusStats(msg);
            NotifyChangedThrottled();
        }
    }

    private void OnMessageDeliveredToAll(Guid messageId, Guid senderId)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null && msg.SenderId == CurrentUserId)
        {
            msg.Status = ClientMessageStatus.Delivered;
            UpdateMessageStatusStats(msg);
            NotifyChangedThrottled();
        }
    }

    private void OnMessageReadToAll(Guid messageId, Guid senderId)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null && msg.SenderId == CurrentUserId)
        {
            msg.Status = ClientMessageStatus.Read;
            UpdateMessageStatusStats(msg);
            NotifyChangedThrottled();
        }
    }

    private void OnMessageReactionUpdated(Guid messageId, Guid userId, int reactionTypeInt, bool isNewReaction)
    {
        var message = Messages.FirstOrDefault(m => m.Id == messageId);
        if (message != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var reactions = await _chatService.GetMessageReactionsAsync(messageId);
                    if (reactions != null)
                    {
                        message.Reactions = reactions;
                        NotifyChanged();
                    }
                }
                catch { }
            });
        }
    }

    // ✅ Helper Methods
    private void RebuildPresenceFromRealtime()
    {
        if (GroupMembers is null) return;

        var onlineSet = _realtime.State.OnlineUsers.ToHashSet();

        OnlineUsers.Clear();
        foreach (var user in GroupMembers.Members
    .Where(m => m.Id != CurrentUserId)
    .Where(m => onlineSet.Contains(m.Id))
    .Select(m => new UserModel
    {
        Id = m.Id,
        DisplayName = m.DisplayName,
        IsOnline = true
    }))
        {
            OnlineUsers.Add(user);
        }
        for (int i = TypingUsers.Count - 1; i >= 0; i--)
        {
            if (!onlineSet.Contains(TypingUsers[i].Id))
            {
                TypingUsers.RemoveAt(i);
            }
        }
        NotifyChanged();
    }

    private UserModel? FindMember(Guid userId)
    {
        return GroupMembers?.Members
            .Where(m => m.Id == userId)
            .Select(m => new UserModel { Id = m.Id, DisplayName = m.DisplayName })
            .FirstOrDefault();
    }

    private async Task RefreshGroupMembersAsync()
    {
        if (Room?.Type == "Group" && _currentRoomId.HasValue)
        {
            var dto = await _chatService.GetGroupMembersAsync(_currentRoomId.Value);
            GroupMembers = new GroupMembersModel
            {
                OwnerId = dto.OwnerId,
                Members = dto.Members.Select(m => new UserModel
                {
                    Id = m.Id,
                    DisplayName = m.DisplayName ?? "User"
                }).ToList()
            };
            RebuildPresenceFromRealtime();
            NotifyChanged();
        }
    }

    private void UpdateMessageStatusStats(MessageModel message)
    {
        if (message.Status == ClientMessageStatus.Delivered)
        {
            message.DeliveredCount = Math.Max(message.DeliveredCount, 1);
        }
        else if (message.Status == ClientMessageStatus.Read)
        {
            message.ReadCount = Math.Max(message.ReadCount, 1);
        }
    }

    private void NotifyChangedThrottled()
    {
        lock (_notifyLock)
        {
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


    private void OnMessagePinned(Guid rid, Guid? mid)
    {
        if (_currentRoomId != rid || Room == null) return;

        // تحديث يدوي لأن RoomModel كلاس مش Record
        Room.PinnedMessageId = mid;

        NotifyChanged("OnMessagePinned");
    }
    public async Task PinMessageAsync(Guid? messageId)
    {
        try
        {
            await _chatService.PinMessageAsync(_currentRoomId!.Value, messageId);
            await _realtime.PinMessageAsync(_currentRoomId!.Value, messageId);
        }
        catch (Exception ex)
        {
            _toasts.Error("Pin failed", ex.Message);
        }
    }
    // ✅ Public Methods
    public async Task SendAsync(Guid roomId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var tempId = Guid.NewGuid();
        var pending = new MessageModel
        {
            Id = tempId,
            RoomId = roomId,
            SenderId = CurrentUserId,
            Content = text,
            CreatedAt = DateTime.UtcNow,
            Status = ClientMessageStatus.Pending
        };

        Messages.Add(pending);
        NotifyChanged();

        try
        {
            var dto = await _chatService.SendMessageAsync(roomId, text);
            if (dto != null)
            {
                pending.Id = dto.Id;
                pending.Status = ClientMessageStatus.Sent;
                pending.Error = null;
            }
            NotifyChanged();
        }
        catch
        {
            pending.Status = ClientMessageStatus.Failed;
            pending.Error = "Failed to send message.";
            _toasts.Error("Send failed", "Network error. Tap retry.");
            NotifyChanged();
        }
    }
    // ✅ SendMessageWithReplyAsync - واحدة فقط بدون تكرار
    public async Task SendMessageWithReplyAsync(
    Guid roomId,
    string text,
    Guid? replyToMessageId,
    ReplyInfoModel? replySnapshot)
    {
        Console.WriteLine($"[VM] SendMessageWithReplyAsync room={roomId} text='{text}' replyTo={replyToMessageId}");

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

        var tempId = Guid.NewGuid();
        var pending = new MessageModel
        {
            Id = tempId,
            RoomId = roomId,
            SenderId = CurrentUserId,
            Content = text,
            CreatedAt = DateTime.UtcNow,
            Status = ClientMessageStatus.Pending,
            ReplyToMessageId = replyToMessageId,
            ReplyInfo = replySnapshot   // ✅ ده اللي هيخلي الشكل يظهر فورًا
        };

        Messages.Add(pending);
        NotifyChanged();

        try
        {
            var dto = await _chatService.SendMessageWithReplyAsync(roomId, text, replySnapshot);
            if (dto != null)
            {
                pending.Id = dto.Id;
                pending.Status = ClientMessageStatus.Sent;
                pending.Error = null;

                // تحويل ReplyInfo
                if (dto.ReplyInfo != null)
                {
                    pending.ReplyInfo = new ReplyInfoModel
                    {
                        MessageId = dto.ReplyInfo.MessageId,
                        SenderId = dto.ReplyInfo.SenderId,
                        SenderName = dto.ReplyInfo.SenderName,
                        ContentPreview = dto.ReplyInfo.ContentPreview,
                        CreatedAt = dto.ReplyInfo.CreatedAt,
                        IsDeleted = dto.ReplyInfo.IsDeleted
                    };
                }

                if (replyToMessageId.HasValue) // فقط لو في reply حقيقي
                {
                    if (_realtime is ChatRealtimeClient realtimeClient)
                    {
                        await realtimeClient.SendMessageWithReplyAsync(roomId, pending);
                    }
                }

                // Trigger حدث الرد
                if (MessageReplyReceived != null)
                {
                    await MessageReplyReceived.Invoke(pending);
                }
            }
            NotifyChanged();
        }
        catch (Exception ex)
        {
            pending.Status = ClientMessageStatus.Failed;
            pending.Error = ex.Message;
            _toasts.Error("Send failed", ex.Message);
            NotifyChanged();
        }
    }

    public async Task NotifyTypingAsync(Guid roomId)
    {
        if (_realtime is ChatRealtimeClient realtimeClient)
        {
            await realtimeClient.NotifyTypingAsync(roomId);
        }
    }
    public async Task NotifyTypingStoppedAsync(Guid roomId)
    {
        try
        {
            await _chatService.StopTypingAsync(roomId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Typing Stop] Error: {ex.Message}");
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
            _flags.SetBlocked(userId, true);
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
            var currentlyMuted = _flags.GetMuted(roomId);
            var nextMuted = !currentlyMuted;

            if (nextMuted)
                await _chatService.MuteAsync(roomId);
            else
                await _chatService.UnmuteAsync(roomId);

            _flags.SetMuted(roomId, nextMuted);
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

    public async Task RefreshMuteStateAsync(Guid roomId)
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

    public async Task AddReactionAsync(Guid messageId, ReactionType reactionType)
    {
        try
        {
            var reactions = await _chatService.ReactToMessageAsync(messageId, reactionType);
            if (reactions != null)
            {
                var message = Messages.FirstOrDefault(m => m.Id == messageId);
                if (message != null)
                {
                    message.Reactions = reactions;
                    NotifyChanged();
                }
            }
        }
        catch (Exception ex)
        {
            _toasts.Error("Reaction failed", ex.Message);
        }
    }

    public string GetSenderName(Guid userId)
    {
        // ✅ لو الرسالة بتاعتك
        if (userId == CurrentUserId)
            return "You";

        // Group members
        var member = GroupMembers?.Members?.FirstOrDefault(m => m.Id == userId);
        if (member != null && !string.IsNullOrWhiteSpace(member.DisplayName))
            return member.DisplayName;

        // Online users
        var online = OnlineUsers.FirstOrDefault(u => u.Id == userId);
        if (online != null && !string.IsNullOrWhiteSpace(online.DisplayName))
            return online.DisplayName;

        // Private other user
        if (OtherUser?.Id == userId && !string.IsNullOrWhiteSpace(OtherUser.DisplayName))
            return OtherUser.DisplayName;

        return "User";
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

    public void NotifyReplyContextChanged(ReplyContext? context)
    {
        ReplyContextChanged?.Invoke(context);
    }

    // ✅ OnRoomMuteChanged and OnUserBlockChanged من الـ Flags
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

    // ✅ DisposeAsync - واحدة فقط
    public async ValueTask DisposeAsync()
    {
        _flags.SetActiveRoom(null);
        _flags.RoomMuteChanged -= OnRoomMuteChanged;
        _flags.UserBlockChanged -= OnUserBlockChanged;

        UnregisterRealtimeEvents();

        if (_currentRoomId != null)
            await _realtime.LeaveRoomAsync(_currentRoomId.Value);

        await _realtime.DisconnectAsync();
        _currentRoomId = null;
    }

    // ✅ IDisposable implementation
    public void Dispose()
    {
        // Sync cleanup if needed
        _flags.RoomMuteChanged -= OnRoomMuteChanged;
        _flags.UserBlockChanged -= OnUserBlockChanged;
        UnregisterRealtimeEvents();
    }
    public void DebugMessages()
    {
        Console.WriteLine($"=== DEBUG MESSAGES ===");
        Console.WriteLine($"Total messages: {Messages.Count}");

        var original = Messages.Where(m => !m.ReplyToMessageId.HasValue).ToList();
        var replies = Messages.Where(m => m.ReplyToMessageId.HasValue).ToList();

        Console.WriteLine($"Original: {original.Count}, Replies: {replies.Count}");

        // تحقق من التكرار
        var duplicateGroups = Messages
            .GroupBy(m => m.Id)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateGroups.Any())
        {
            Console.WriteLine("⚠️ DUPLICATE MESSAGES FOUND!");
            foreach (var group in duplicateGroups)
            {
                Console.WriteLine($"  Message {group.Key} appears {group.Count()} times");
            }
        }

        // تحقق من التكرار بالمحتوى
        var contentDuplicates = Messages
            .GroupBy(m => new { m.Content, m.SenderId, m.CreatedAt })
            .Where(g => g.Count() > 1)
            .ToList();

        if (contentDuplicates.Any())
        {
            Console.WriteLine("⚠️ CONTENT DUPLICATES FOUND!");
            foreach (var group in contentDuplicates)
            {
                Console.WriteLine($"  '{group.Key.Content}' appears {group.Count()} times");
            }
        }
    }
    private async Task PerformSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        // تغيير الشرط ليسمح بحرف واحد (بشرط ميكونش مسافة فاضية)
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            return;
        }

        try
        {
            await Task.Delay(300, ct);
            var results = await _chatService.SearchMessagesAsync(Room!.Id, SearchQuery);

            SearchResults.Clear();
            foreach (var res in results) SearchResults.Add(res);
            NotifyChanged();
        }
        catch (OperationCanceledException) { }
    }

    private void OnMessageUpdated(Guid messageId, string newContent)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            msg.Content = newContent;
            msg.IsEdited = true;
            NotifyChanged();
        }
    }

    private void OnMessageDeleted(Guid messageId)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            msg.IsDeleted = true;
            msg.Content = "This message was deleted";
            NotifyChanged();
        }
    }

    public async Task EditMessageAsync(Guid messageId, string newContent)
    {
        try
        {
            await _chatService.EditMessageAsync(messageId, newContent);
        }
        catch (Exception ex)
        {
            _toasts.Error("Edit failed", ex.Message);
        }
    }

    public async Task DeleteMessageAsync(Guid messageId, bool forEveryone)
    {
        try
        {
            await _chatService.DeleteMessageAsync(messageId, forEveryone);
        }
        catch (Exception ex)
        {
            _toasts.Error("Delete failed", ex.Message);
        }
    }

    public async Task PinMessageWithDurationAsync(Guid messageId, string duration)
    {
        await _chatService.PinMessageAsync(Room.Id, messageId, duration);
        // السيرفر هيبعت SignalR يضيف رسالة نظام تلقائياً في الشات
        // والـ TopBar هيحدث نفسه فوراً
    }
}
