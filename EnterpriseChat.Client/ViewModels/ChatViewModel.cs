using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Components.Rooms;
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
    private readonly RoomsViewModel _roomsVM;
        public event Action? Changed;
    public event Func<MessageModel, Task>? MessageReceived;
    public event Func<MessageModel, Task>? MessageUpdated;
    public event Func<MessageModel, Task>? MessageReplyReceived;
    public event Action<ReplyContext?>? ReplyContextChanged;
    private Guid? _openMenuMessageId;
    private readonly GroupsApi _groupsApi;     public event PropertyChangedEventHandler? PropertyChanged;
    private bool _isReplaceModalOpen;
    public bool IsReplaceModalOpen { get => _isReplaceModalOpen; set { _isReplaceModalOpen = value; NotifyChanged(); } }
    private bool _isPinModalOpen;
    public bool IsPinModalOpen { get => _isPinModalOpen; set { _isPinModalOpen = value; NotifyChanged(); } }
    private Guid? _messageIdToPin;
    public bool IsMessagePinned(Guid messageId) => PinnedMessages.Any(m => m.Id == messageId);
    public ObservableCollection<MessageModel> PinnedMessages { get; } = new();

    private bool _isSelectionMode;

    public event Action? SelectionModeChanged;

    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            _isSelectionMode = value;
            if (!value) SelectedMessageIds.Clear();
            SelectionModeChanged?.Invoke();             NotifyChanged();
        }
    }
   
    public ObservableCollection<Guid> SelectedMessageIds { get; } = new();

    public Guid? PinnedMessageId => PinnedMessages.LastOrDefault()?.Id;
    public MessageModel? PinnedMessage => PinnedMessages.LastOrDefault();
    public void OpenPinModal(Guid messageId)
    {
        if (IsMessagePinned(messageId))
        {
            _ = UnpinMessageAsync(messageId);
            return;
        }

        _messageIdToPin = messageId;

                if (PinnedMessages.Count >= 3)
        {
            IsReplaceModalOpen = true;
            NotifyChanged();
            return;
        }

        IsPinModalOpen = true;
        NotifyChanged();
    }        public void ToggleMessageSelection(Guid messageId)
    {
        if (SelectedMessageIds.Contains(messageId))
            SelectedMessageIds.Remove(messageId);
        else
            SelectedMessageIds.Add(messageId);
        NotifyChanged();
    }

        private bool _isForwardModalOpen;
    public bool IsForwardModalOpen
    {
        get => _isForwardModalOpen;
        set { _isForwardModalOpen = value; NotifyChanged(); }
    }
    public async Task ConfirmPinAsync(string duration)
    {
        if (!_messageIdToPin.HasValue) return;
        var msgId = _messageIdToPin.Value;
        IsPinModalOpen = false;
        IsReplaceModalOpen = false;

        try
        {
            await _chatService.PinMessageAsync(Room!.Id, msgId, duration);

            var msg = Messages.FirstOrDefault(m => m.Id == msgId);
            if (msg != null && !PinnedMessages.Any(x => x.Id == msgId))
            {
                if (PinnedMessages.Count >= 3)
                    PinnedMessages.RemoveAt(0);
                PinnedMessages.Add(msg);
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
        var msg = PinnedMessages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            PinnedMessages.Remove(msg);
            NotifyChanged();
        }

        try
        {
                        await _chatService.PinMessageAsync(Room!.Id, null, null, messageId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error unpinning: {ex.Message}");
            if (msg != null && !PinnedMessages.Any(m => m.Id == messageId))
                PinnedMessages.Add(msg);
            NotifyChanged();
        }
    }
    public async Task<bool> ExecuteForwardAsync(List<Guid> targetRoomIds)
    {
        if (!SelectedMessageIds.Any() || !targetRoomIds.Any()) return false;

        try
        {
                        var request = new ForwardMessagesRequest
            {
                MessageIds = SelectedMessageIds.ToList(),
                TargetRoomIds = targetRoomIds
            };

                        await _chatService.ForwardMessagesAsync(request);

                        IsSelectionMode = false;

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

                        if (messageId == null)
            {
                PinnedMessages.Clear();             }
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

        public ChatViewModel(
        IChatService chatService,
        IRoomService roomService,
        IChatRealtimeClient realtime,
        ICurrentUser currentUser,
        ToastService toasts,
        RoomFlagsStore flags,
        ModerationApi mod, ReactionsApi reactionsApi,
         RoomsViewModel roomsVM, GroupsApi groupsApi)
    {
        _chatService = chatService;
        _roomService = roomService;
        _realtime = realtime;
        _currentUser = currentUser;
        _toasts = toasts;
        _flags = flags;
        _mod = mod;
        _reactionsApi = reactionsApi;
        _roomsVM = roomsVM;
        _groupsApi = groupsApi;
    }

        public RoomModel? Room { get; private set; }
    public GroupMembersModel? GroupMembers { get; private set; }
    public UserModel? OtherUser { get; private set; }
    public ObservableCollection<MessageModel> Messages { get; } = new();
    public ObservableCollection<UserModel> OnlineUsers { get; } = new();
    public ObservableCollection<UserModel> TypingUsers { get; } = new();
    public string? UiError { get; private set; }
    public Guid CurrentUserId { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsBlockedByMe { get;  set; }
    public bool IsBlockedMe { get;  set; }
    public bool IsDisconnected { get; private set; }
    public bool IsOtherDeleted { get; private set; }
    public bool IsRemoved { get; private set; }
    private string _searchQuery = string.Empty;
    public string SearchQuery { get => _searchQuery; set { _searchQuery = value; NotifyChanged(); _ = PerformSearchAsync(); } }
    public bool IsPageActive { get; set; }     private bool _isSearching;
    public bool IsSearching { get => _isSearching; set { _isSearching = value; if (!value) SearchResults.Clear(); NotifyChanged(); } }
    private readonly Dictionary<Guid, (int total, int delivered, int read)> _pendingStats = new();

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

            public async Task InitializeAsync(Guid roomId)
    {
                UnregisterRealtimeEvents();
        _flags.RoomMuteChanged -= OnRoomMuteChanged;
        _flags.BlockedByMeChanged -= OnBlockedByMeChanged;
        _flags.BlockedMeChanged -= OnBlockedMeChanged;

        TypingUsers.Clear();
        OnlineUsers.Clear();
        Messages.Clear();
        PinnedMessages.Clear();
        IsRemoved = false;
        IsOtherDeleted = false;
        UiError = null;
                await _flags.LoadStateAsync(_mod, _currentUser);

        Console.WriteLine($"[Flags] Loaded muted rooms from server");
        _currentRoomId = roomId;
        _flags.SetActiveRoom(roomId);
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

                        IsMuted = _flags.GetMuted(roomId);
            _flags.SetMuted(roomId, IsMuted);

            Console.WriteLine($"[Init] ✅ IsMuted loaded from FLAGS (SERVER) = {IsMuted}");
                        var rawMessages = await _chatService.GetMessagesAsync(roomId, CurrentUserId, 0, 200);
            var uniqueMessages = rawMessages
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .OrderBy(m => m.CreatedAt)
                .ToList();

                        if (Room.Type == "Group")
            {
                var dto = await _chatService.GetGroupMembersAsync(roomId);
                GroupMembers = new GroupMembersModel
                {
                    OwnerId = dto.OwnerId,
                    Members = dto.Members.Select(m => new UserModel
                    {
                        Id = m.Id,
                        DisplayName = m.DisplayName ?? "User",
                        IsAdmin = m.IsAdmin
                    }).ToList()
                };
                foreach (var member in GroupMembers.Members)
                {
                    if (!string.IsNullOrEmpty(member.DisplayName))
                        _roomsVM.CacheMemberName(member.Id, member.DisplayName);
                }
            }
            else if (Room.Type == "Private")
            {
                if (Room.OtherUserId == null) IsOtherDeleted = true;
                else
                {
                    OtherUser = new UserModel { Id = Room.OtherUserId.Value, DisplayName = Room.OtherDisplayName ?? "User" };
                    var myBlocks = await _mod.GetBlockedAsync();
                    var blockedMe = await _mod.GetBlockedByMeAsync();
                    IsBlockedByMe = myBlocks.Any(b => b.UserId == OtherUser.Id);
                    IsBlockedMe = blockedMe.Any(b => b.UserId == OtherUser.Id);
                    _flags.SetBlockedByMe(OtherUser.Id, IsBlockedByMe);
                    _flags.SetBlockedMe(OtherUser.Id, IsBlockedMe);
                }
            }

                        int memberCount = Room.Type == "Group" ? (GroupMembers?.Members.Count ?? 1) - 1 : 1;

            foreach (var msg in uniqueMessages)
            {
                msg.TotalRecipients = memberCount;
                Messages.Add(msg);
            }

                        PinnedMessages.Clear();
            try
            {
                var pinnedIds = await _chatService.GetPinnedMessagesAsync(roomId);
                foreach (var pinId in pinnedIds)
                {
                    var msg = Messages.FirstOrDefault(m => m.Id == pinId);
                    if (msg != null && !PinnedMessages.Any(p => p.Id == pinId))
                        PinnedMessages.Add(msg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Pins] GetPinnedMessagesAsync failed: {ex.Message}");
            }

                        if (!PinnedMessages.Any() && Room.PinnedMessageId != null)
            {
                var pinnedMsg = Messages.FirstOrDefault(m => m.Id == Room.PinnedMessageId);
                if (pinnedMsg != null) PinnedMessages.Add(pinnedMsg);
            }

                        _flags.RoomMuteChanged += OnRoomMuteChanged;
            _flags.BlockedByMeChanged += OnBlockedByMeChanged;
            _flags.BlockedMeChanged += OnBlockedMeChanged;

            RegisterRealtimeEvents(roomId);
            await _realtime.ConnectAsync();
            await _realtime.JoinRoomAsync(roomId);

            _flags.SetUnread(roomId, 0);

            if (Messages.Any())
            {
                var last = Messages.Last();
                if (last.SenderId != CurrentUserId)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(800);
                        try { await MarkRoomReadAsync(roomId, last.Id); } catch { }
                    });
                }
            }

            if (Room.Type == "Private" && OtherUser != null)
            {
                var onlineSet = _realtime.State.OnlineUsers?.ToHashSet() ?? new HashSet<Guid>();
                OtherUser.IsOnline = onlineSet.Contains(OtherUser.Id);
            }
            else if (Room.Type == "Group")
            {
                RebuildPresenceFromRealtime();
            }

            NotifyChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Init Error] {ex.Message}");
            UiError = "Failed to load chat. Please try again.";
            NotifyChanged();
        }
    }

    private string _currentPage = "";
    public string CurrentPage
    {
        get => _currentPage;
        set
        {
            _currentPage = value;
            NotifyChanged();
        }
    }
 
    private async void OnAdminPromoted(Guid roomId, Guid userId)
    {
        if (_currentRoomId != roomId) return;

        if (GroupMembers != null)
        {
            var member = GroupMembers.Members.FirstOrDefault(m => m.Id == userId);
            if (member != null)
            {
                member.IsAdmin = true;
                Console.WriteLine($"[VM] ✅ User {member.DisplayName} promoted to Admin");
                NotifyChanged();
            }
        }
    }

    private async void OnAdminDemoted(Guid roomId, Guid userId)
    {
        if (_currentRoomId != roomId) return;

        if (GroupMembers != null)
        {
            var member = GroupMembers.Members.FirstOrDefault(m => m.Id == userId);
            if (member != null)
            {
                member.IsAdmin = false;
                Console.WriteLine($"[VM] ✅ User {member.DisplayName} demoted from Admin");
                NotifyChanged();
            }
        }
    }
        private void RegisterRealtimeEvents(Guid roomId)
    {
        if (_eventsRegistered && _eventsRoomId == roomId)
            return;

        UnregisterRealtimeEvents();
        _eventsRoomId = roomId;
        Console.WriteLine($"[VM] Registering realtime events for room {roomId}");         _realtime.MessageReceived += OnRealtimeMessageReceived;
        _realtime.MessageDelivered += OnMessageDelivered;
        _realtime.MessageRead += OnMessageRead;
        _realtime.MessageDeliveredToAll += OnMessageDeliveredToAll;
        _realtime.MessageReadToAll += OnMessageReadToAll;
        _realtime.RoomMuteChanged += OnRealtimeRoomMuteChanged;
        _realtime.UserOnline += OnUserOnline;
        _realtime.UserOffline += OnUserOffline;
        _realtime.UserLastSeenUpdated += OnUserLastSeenUpdated;
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
        _realtime.MemberRoleChanged += OnMemberRoleChanged;
        _realtime.MessageReactionUpdated += OnMessageReactionUpdated;
        _realtime.MessageUpdated += OnMessageUpdated;
        _realtime.MessageDeleted += OnMessageDeleted;
        _realtime.RoomUpdated += OnRealtimeRoomUpdated;

        _realtime.MessagePinned += OnMessagePinned;
        _realtime.AdminPromoted += OnAdminPromoted;
        _realtime.AdminDemoted += OnAdminDemoted;
        _realtime.ChatCleared += OnChatCleared;
        _realtime.OnDemandOnlineCheckRequested += HandleOnDemandCheck;
        _realtime.MessageReceiptStatsUpdated += OnMessageReceiptStatsUpdated;
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
        _realtime.OnDemandOnlineCheckRequested -= HandleOnDemandCheck;
        _realtime.MessageReceiptStatsUpdated -= OnMessageReceiptStatsUpdated;
        _realtime.RoomUpdated -= OnRealtimeRoomUpdated;

        _realtime.MemberRoleChanged -= OnMemberRoleChanged;
        _realtime.AdminPromoted -= OnAdminPromoted;
        _realtime.AdminDemoted -= OnAdminDemoted;
        _realtime.ChatCleared -= OnChatCleared;
        _realtime.MessageDeleted -= OnMessageDeleted;
        _eventsRegistered = false;
        _eventsRoomId = null;
    }
    private void OnMemberRoleChanged(Guid roomId, Guid userId, bool isAdmin)
    {
        if (_currentRoomId != roomId) return;

        var member = GroupMembers?.Members.FirstOrDefault(m => m.Id == userId);
        if (member != null)
        {
            member.IsAdmin = isAdmin;
            NotifyChanged("MemberRoleChanged via SignalR");
        }
    }
        private void OnMessageReadToAll(Guid messageId, Guid senderId, Guid roomId)
    {
        Console.WriteLine($"[VM] OnMessageReadToAll: msg={messageId}, sender={senderId}, room={roomId}");
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg == null) return;

        if (msg.SenderId == CurrentUserId)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var stats = await _chatService.GetMessageStatsAsync(messageId);
                    if (stats != null)
                    {
                        msg.ReadCount = stats.ReadCount;
                        msg.DeliveredCount = stats.DeliveredCount;
                        msg.TotalRecipients = stats.TotalRecipients;

                        if (msg.ReadCount >= msg.TotalRecipients)
                            msg.PersonalStatus = ClientMessageStatus.Read;
                        else if (msg.DeliveredCount > 0)
                            msg.PersonalStatus = ClientMessageStatus.Delivered;
                        else
                            msg.PersonalStatus = ClientMessageStatus.Sent;

                        NotifyChanged();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VM] Error getting stats: {ex.Message}");
                }
            });
        }
        else
        {
            if (senderId == CurrentUserId)
            {
                msg.PersonalStatus = ClientMessageStatus.Read;
                NotifyChanged();
            }
        }
    }
    private async void HandleOnDemandCheck(Guid userId)
    {
        try
        {
                        var onlineUsers = await _realtime.GetOnlineUsersAsync();

            if (onlineUsers.Contains(userId))
            {
                                                OnUserOnline(userId);
                NotifyChanged("HandleOnDemandCheck");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Check Error] {ex.Message}");
        }
    }

    private void OnMessageDelivered(Guid messageId, Guid roomId)
    {
        Console.WriteLine($"[VM] OnMessageDelivered received for msg {messageId} in room {roomId}");
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            msg.PersonalStatus = ClientMessageStatus.Delivered;
            UpdateMessageStatusStats(msg);
            MessageUpdated?.Invoke(msg);
            NotifyChangedThrottled();
            Console.WriteLine($"[VM] Updated msg {messageId} to Delivered (PersonalStatus)");
        }
        else
        {
            Console.WriteLine($"[VM] Message {messageId} not found in local list");
        }
    }
    private void OnMessageRead(Guid messageId, Guid roomId)
    {
        Console.WriteLine($"[VM] OnMessageRead received for msg {messageId} in room {roomId}");
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            msg.IsConfirmedRead = true;
            msg.PersonalStatus = ClientMessageStatus.Read;
            msg.ReadCount = Math.Max(msg.ReadCount, 1);
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
        {
            RebuildPresenceFromRealtime();
        }
        NotifyChangedThrottled();
    }

    private void OnUserOffline(Guid id)
    {
        Console.WriteLine($"[VM] UserOffline received for {id}");

                var userToRemove = OnlineUsers.FirstOrDefault(u => u.Id == id);
        if (userToRemove != null)
        {
            OnlineUsers.Remove(userToRemove);
            Console.WriteLine($"[VM] Removed user {id} from OnlineUsers. New count: {OnlineUsers.Count}");
        }

                if (OtherUser?.Id == id)
        {
            OtherUser.IsOnline = false;
            OtherUser.LastSeen = DateTime.UtcNow;
        }

                var typingUser = TypingUsers.FirstOrDefault(u => u.Id == id);
        if (typingUser != null)
        {
            TypingUsers.Remove(typingUser);
        }

                NotifyChanged(); 
                if (Room?.Type == "Group")
        {
                        RebuildPresenceFromRealtime();
        }
    }
    private void OnUserLastSeenUpdated(Guid userId, DateTime lastSeen)
    {
        if (OtherUser?.Id == userId)
        {
            OtherUser.LastSeen = lastSeen;
            Console.WriteLine($"[VM] Last seen updated for {userId}: {lastSeen}");
            NotifyChangedThrottled();
        }
    }

    private void OnRoomPresenceUpdated(Guid rid, int count)
    {
        if (_currentRoomId == rid && Room?.Type == "Group")
        {
            Console.WriteLine($"[VM] Room {rid} presence updated: {count} online");

                        RebuildPresenceFromRealtime();

            NotifyChanged();
        }
    }
            private void OnTypingStarted(Guid rid, Guid uid)
    {
        if (_eventsRoomId != rid || uid == CurrentUserId) return;

        Console.WriteLine($"[VM] 🔍 OnTypingStarted received - Room: {rid}, User: {uid}");

                var existing = TypingUsers.FirstOrDefault(u => u.Id == uid);
        if (existing != null)
        {
            Console.WriteLine($"[VM] User {uid} already typing, skipping");
            return;
        }

                UserModel? user = null;

                if (Room?.Type == "Group" && GroupMembers != null)
        {
            var member = GroupMembers.Members.FirstOrDefault(m => m.Id == uid);
            if (member != null)
            {
                user = new UserModel
                {
                    Id = member.Id,
                    DisplayName = member.DisplayName ?? "User",
                    IsOnline = true
                };
                Console.WriteLine($"[VM] Found user in GroupMembers: {member.DisplayName}");
            }
        }
                else if (Room?.Type == "Private" && OtherUser != null && OtherUser.Id == uid)
        {
            user = new UserModel
            {
                Id = OtherUser.Id,
                DisplayName = OtherUser.DisplayName ?? "User",
                IsOnline = OtherUser.IsOnline
            };
            Console.WriteLine($"[VM] Found user in OtherUser: {OtherUser.DisplayName}");
        }

                if (user == null)
        {
            var name = GetSenderName(uid);
            user = new UserModel
            {
                Id = uid,
                DisplayName = name,
                IsOnline = true
            };
            Console.WriteLine($"[VM] Using GetSenderName: {name}");
        }

                TypingUsers.Add(user);
        Console.WriteLine($"[VM] ✅ Added {user.DisplayName} to TypingUsers. Total: {TypingUsers.Count}");

                NotifyChanged();
    }
    
            public IReadOnlyList<UserModel> GetAllUsers()
    {
        var users = new List<UserModel>();

                if (GroupMembers?.Members != null)
        {
            foreach (var member in GroupMembers.Members)
            {
                if (!users.Any(u => u.Id == member.Id))
                {
                    users.Add(new UserModel
                    {
                        Id = member.Id,
                        DisplayName = member.DisplayName ?? "User",
                        IsOnline = OnlineUsers.Any(u => u.Id == member.Id)
                    });
                }
            }
        }

                if (OtherUser != null && !users.Any(u => u.Id == OtherUser.Id))
        {
            users.Add(OtherUser);
        }

                if (!users.Any(u => u.Id == CurrentUserId))
        {
            users.Add(new UserModel
            {
                Id = CurrentUserId,
                DisplayName = "You",
                IsOnline = true
            });
        }

        return users;
    }
    private void OnTypingStopped(Guid rid, Guid uid)
    {
        if (_eventsRoomId != rid) return;

        var removed = false;
        var userToRemove = TypingUsers.FirstOrDefault(u => u.Id == uid);
        if (userToRemove != null)
        {
            removed = TypingUsers.Remove(userToRemove);
        }

        if (removed)
        {
            Console.WriteLine($"[VM] ✋ TypingStopped: User {uid} removed. Total typing: {TypingUsers.Count}");
            NotifyChanged();
        }
    }
    private void OnDisconnected()
    {
        IsDisconnected = true;

                OnlineUsers.Clear();
        TypingUsers.Clear();

        if (OtherUser != null)
        {
            OtherUser.IsOnline = false;
            OtherUser.LastSeen = DateTime.UtcNow;
        }

                NotifyChanged();
    }
    private void OnReconnected()
    {
        IsDisconnected = false;

                if (_currentRoomId.HasValue)
        {
            IsMuted = _flags.GetMuted(_currentRoomId.Value);
            Console.WriteLine($"[VM] 🔄 Reconnected - Synced IsMuted to {IsMuted}");
        }

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

                NotifyChanged("OnGroupRenamed");
    }
    private async void OnMemberAdded(Guid roomId, Guid userId, string displayName)
    {
        if (_currentRoomId != roomId) return;
        await RefreshGroupMembersAsync();
        RebuildPresenceFromRealtime();
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


    private void OnMessageDeliveredToAll(Guid messageId, Guid senderId, Guid roomId)
    {
        Console.WriteLine($"[VM] OnMessageDeliveredToAll: msg={messageId}, sender={senderId}, room={roomId}");
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg == null) return;

        if (msg.SenderId == CurrentUserId)
        {
            msg.DeliveredCount = Math.Max(msg.DeliveredCount, 1);
            if (msg.PersonalStatus < ClientMessageStatus.Delivered)
                msg.PersonalStatus = ClientMessageStatus.Delivered;
            UpdateMessageStatusStats(msg);
            NotifyChangedThrottled();
        }
    }


    public async Task RefreshMessageReceiptsAsync(Guid messageId)
    {
        var stats = await _chatService.GetMessageStatsAsync(messageId);
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null && stats != null)
        {
            msg.DeliveredCount = stats.DeliveredCount;
            msg.ReadCount = stats.ReadCount;
            msg.TotalRecipients = stats.TotalRecipients;
            NotifyChanged();
        }
    }
        private void OnMessageReactionUpdated(Guid messageId, Guid userId, int reactionTypeInt, bool isNewReaction)
    {
        var message = Messages.FirstOrDefault(m => m.Id == messageId);
        if (message == null) return;

        var reactionType = (ReactionType)reactionTypeInt;

                var newCounts = message.Reactions?.Counts != null
            ? new Dictionary<ReactionType, int>(message.Reactions.Counts)
            : new Dictionary<ReactionType, int>();

        var currentUserReactionType = message.Reactions?.CurrentUserReactionType;
        var currentUserReaction = message.Reactions?.CurrentUserReaction;

        if (isNewReaction)
        {
                        if (userId == CurrentUserId && currentUserReactionType.HasValue
                && currentUserReactionType.Value != reactionType)
            {
                var oldType = currentUserReactionType.Value;
                if (newCounts.ContainsKey(oldType))
                {
                    newCounts[oldType]--;
                    if (newCounts[oldType] <= 0)
                        newCounts.Remove(oldType);
                }
            }

                        if (newCounts.ContainsKey(reactionType))
                newCounts[reactionType]++;
            else
                newCounts[reactionType] = 1;

            if (userId == CurrentUserId)
            {
                currentUserReactionType = reactionType;
                currentUserReaction = userId;
            }
        }
        else
        {
                        if (newCounts.ContainsKey(reactionType))
            {
                newCounts[reactionType]--;
                if (newCounts[reactionType] <= 0)
                    newCounts.Remove(reactionType);
            }

            if (userId == CurrentUserId)
            {
                currentUserReactionType = null;
                currentUserReaction = null;
            }
        }

                        message.Reactions = newCounts.Any()
            ? new MessageReactionsModel
            {
                MessageId = messageId,
                Counts = newCounts,
                CurrentUserReactionType = currentUserReactionType,
                CurrentUserReaction = currentUserReaction
            }
            : null;

        NotifyChanged();
    }        private void RebuildPresenceFromRealtime()
    {
        if (GroupMembers is null)
        {
            Console.WriteLine("[Rebuild] GroupMembers is null, skipping");
            return;
        }

        var onlineSet = _realtime.State.OnlineUsers.ToHashSet();
        Console.WriteLine($"[Rebuild] Raw online users from SignalR ({_realtime.State.OnlineUsers.Count}): {string.Join(", ", _realtime.State.OnlineUsers)}");

                        OnlineUsers.Clear();
        int added = 0;

        foreach (var member in GroupMembers.Members.Where(m => m.Id != CurrentUserId))
        {
            if (onlineSet.Contains(member.Id))
            {
                OnlineUsers.Add(new UserModel
                {
                    Id = member.Id,
                    DisplayName = member.DisplayName ?? "Unknown",
                    IsOnline = true
                });
                added++;
            }
        }

                for (int i = TypingUsers.Count - 1; i >= 0; i--)
        {
            if (!onlineSet.Contains(TypingUsers[i].Id))
            {
                Console.WriteLine($"[Rebuild] Removed typing user {TypingUsers[i].Id} - no longer online");
                TypingUsers.RemoveAt(i);
            }
        }

        Console.WriteLine($"[Rebuild] Final result: {added} online users");

                NotifyChanged();     }
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
        if (message.PersonalStatus == ClientMessageStatus.Delivered)
            message.DeliveredCount = Math.Max(message.DeliveredCount, 1);
        else if (message.PersonalStatus == ClientMessageStatus.Read)
            message.ReadCount = Math.Max(message.ReadCount, 1);
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


    private async void OnMessagePinned(Guid rid, Guid? mid)
    {
        if (_currentRoomId != rid || Room == null) return;

        Room.PinnedMessageId = mid;

        if (mid != null)
        {
                        var msg = Messages.FirstOrDefault(m => m.Id == mid);
            if (msg != null && !PinnedMessages.Any(m => m.Id == mid))
            {
                if (PinnedMessages.Count >= 3)
                    PinnedMessages.RemoveAt(0);
                PinnedMessages.Add(msg);
            }
        }
        else
        {
                        PinnedMessages.Clear();
            try
            {
                var pinnedIds = await _chatService.GetPinnedMessagesAsync(rid);
                foreach (var pinId in pinnedIds)
                {
                    var msg = Messages.FirstOrDefault(m => m.Id == pinId);
                    if (msg != null && !PinnedMessages.Any(p => p.Id == pinId))
                        PinnedMessages.Add(msg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnMessagePinned] Reload pins failed: {ex.Message}");
            }
        }

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
        public async Task SendAsync(Guid roomId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

                int currentMemberCount = Room?.Type == "Group" ? (GroupMembers?.Members.Count ?? 1) - 1 : 1;

        var tempId = Guid.NewGuid();
        var pending = new MessageModel
        {
            Id = tempId,
            Content = text,
            Status = ClientMessageStatus.Pending,
            SenderId = CurrentUserId,
            CreatedAt = DateTime.UtcNow,
            TotalRecipients = currentMemberCount         };

        var list = Messages.ToList();
        list.Add(pending);
        var sorted = list.OrderBy(m => m.CreatedAt).ToList();

        Messages.Clear();
        foreach (var msg in sorted)
        {
            Messages.Add(msg);
        }

        NotifyChanged();

        try
        {
            var dto = await _chatService.SendMessageAsync(roomId, text);
            if (dto != null)
            {
                pending.Id = dto.Id;
                pending.Status = ClientMessageStatus.Sent;
                NotifyChanged();
            }
        }
        catch (Exception ex)
        {
            pending.Status = ClientMessageStatus.Failed;
            pending.Error = "Failed to send";
            NotifyChanged();
        }
    }

        public async Task SendMessageWithReplyAsync(Guid roomId, string text, Guid? replyToMessageId, ReplyInfoModel? replySnapshot)
    {
        Console.WriteLine($"[VM] SendMessageWithReplyAsync room={roomId} text='{text}' replyTo={replyToMessageId}");

        if (IsMuted) { _toasts.Warning("Muted", "This chat is muted."); return; }
        if (IsBlockedByMe) { _toasts.Warning("Blocked", "You blocked this user."); return; }
        if (IsOtherDeleted) { _toasts.Warning("Unavailable", "This user is no longer available."); return; }
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 2000) { _toasts.Warning("Too long", "Message is too long."); return; }

        int currentMemberCount = Room?.Type == "Group" ? (GroupMembers?.Members.Count ?? 1) - 1 : 1;

        var tempId = Guid.NewGuid();
        var pending = new MessageModel
        {
            Id = tempId,
            RoomId = roomId,
            SenderId = CurrentUserId,
            Content = text,
            CreatedAt = DateTime.UtcNow,
            Status = ClientMessageStatus.Sent,
            PersonalStatus = ClientMessageStatus.Sent,
            ReplyToMessageId = replyToMessageId,
            ReplyInfo = replySnapshot,
            TotalRecipients = currentMemberCount,
            IsSystemMessage = false
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

                                if (dto.CreatedAt != default)
                    pending.CreatedAt = dto.CreatedAt;

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

                                var sorted = Messages.OrderBy(m => m.CreatedAt).ToList();
                Messages.Clear();
                foreach (var msg in sorted)
                    Messages.Add(msg);
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
        Console.WriteLine($"[VM] MarkRoomReadAsync: room={roomId}, lastMsg={lastMessageId}");

        try
        {
                        await _realtime.MarkRoomReadAsync(roomId, lastMessageId);
            Console.WriteLine($"[VM] MarkRoomRead via SignalR successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VM] SignalR MarkRoomRead failed: {ex.Message}");
            try
            {
                                await _chatService.MarkRoomReadAsync(roomId, lastMessageId);
                Console.WriteLine($"[VM] MarkRoomRead via HTTP successful");
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"[VM] HTTP MarkRoomRead failed: {ex2.Message}");
            }
        }

                _flags.SetUnread(roomId, 0);
        Console.WriteLine($"[VM] Local unread reset to 0");
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
            await _mod.BlockAsync(userId);
            IsBlockedByMe = true;                                _flags.SetBlockedByMe(userId, true);                 TypingUsers.Clear();
            if (OtherUser?.Id == userId)
            {
                OtherUser.IsOnline = false;
                OtherUser.LastSeen = null;
            }
            NotifyChanged();
        }
        catch (Exception ex)
        {
            _toasts.Error("Block failed", ex.Message);
        }
    }
    public async Task UnblockUserAsync(Guid userId)
    {
        try
        {
            await _mod.UnblockAsync(userId);
            IsBlockedByMe = false;
            _flags.SetBlockedByMe(userId, false);

            if (OtherUser?.Id == userId)
            {
                var onlineUsers = await _realtime.GetOnlineUsersAsync();
                OtherUser.IsOnline = onlineUsers.Contains(userId);

                if (OtherUser.IsOnline)
                {
                    OtherUser.LastSeen = null;
                }
                else
                {
                    try
                    {
                        var status = await _realtime.GetUserOnlineStatus(userId);
                        if (status != null)
                        {
                            var lastSeen = (DateTime?)status.GetType().GetProperty("LastSeen")?.GetValue(status);
                            OtherUser.LastSeen = lastSeen;
                        }
                    }
                    catch
                    {
                        OtherUser.LastSeen = null;                      }
                }
            }

            NotifyChanged("Unblock completed - status refreshed");
        }
        catch (Exception ex)
        {
            _toasts.Error("Unblock failed", ex.Message);
        }
    }
    public async Task ToggleMuteAsync(Guid roomId)
    {
        try
        {
            var nextMuted = !IsMuted;

                        IsMuted = nextMuted;
            _flags.SetMuted(roomId, nextMuted);
            NotifyChanged();

                        if (nextMuted)
                await _chatService.MuteAsync(roomId);
            else
                await _chatService.UnmuteAsync(roomId);

            Console.WriteLine($"[Mute Toggle] ✅ Changed to {nextMuted}");
        }
        catch (Exception ex)
        {
            _toasts.Error("Failed", $"Could not toggle mute: {ex.Message}");

                        IsMuted = !IsMuted;
            _flags.SetMuted(roomId, IsMuted);
            await RefreshMuteStateAsync(roomId);
            NotifyChanged();
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
                IsMuted = _flags.GetMuted(roomId);
            }

                        var membersDto = await _groupsApi.GetMembersAsync(roomId);
            if (membersDto != null)
            {
                Console.WriteLine($"[RefreshRoomState] Got {membersDto.Members.Count} members from API");

                                var memberList = membersDto.Members.Select(m => new UserModel
                {
                    Id = m.Id,
                    DisplayName = m.DisplayName ?? "User",
                    Email = "",
                    IsAdmin = m.IsAdmin                 }).ToList();

                                foreach (var m in memberList)
                {
                    Console.WriteLine($"[RefreshRoomState] Member {m.DisplayName} - IsAdmin stored: {m.IsAdmin}");
                }

                                this.GroupMembers = new GroupMembersModel
                {
                    OwnerId = membersDto.OwnerId,
                    Members = memberList
                };

                Console.WriteLine($"[RefreshRoomState] GroupMembers updated with {memberList.Count} members");
            }

                        NotifyChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatViewModel] Refresh failed: {ex.Message}");
        }
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
                                    OnMessageReactionUpdated(messageId, CurrentUserId, (int)reactionType, true);

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
                        var reactions = await _chatService.GetMessageReactionsAsync(messageId);
            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                message.Reactions = reactions;
                NotifyChanged();
            }
        }
    }
    public string GetSenderName(Guid userId)
    {
                if (userId == CurrentUserId)
            return "You";

                var member = GroupMembers?.Members?.FirstOrDefault(m => m.Id == userId);
        if (member != null && !string.IsNullOrWhiteSpace(member.DisplayName))
            return member.DisplayName;

                var online = OnlineUsers.FirstOrDefault(u => u.Id == userId);
        if (online != null && !string.IsNullOrWhiteSpace(online.DisplayName))
            return online.DisplayName;

                if (OtherUser?.Id == userId && !string.IsNullOrWhiteSpace(OtherUser.DisplayName))
            return OtherUser.DisplayName;

        return "User";
    }

    public IReadOnlyList<UserModel> GetAllMembersForDrawer()
    {
        if (GroupMembers?.Members == null) return Array.Empty<UserModel>();

        var onlineSet = _realtime.State.OnlineUsers.ToHashSet();

                foreach (var m in GroupMembers.Members)
        {
            m.IsOnline = onlineSet.Contains(m.Id);
        }

        return GroupMembers.Members
            .OrderByDescending(u => u.Id == GroupMembers.OwnerId)             .ThenByDescending(u => u.IsAdmin)             .ThenByDescending(u => u.IsOnline)             .ToList();
    }
    public void NotifyReplyContextChanged(ReplyContext? context)
    {
        ReplyContextChanged?.Invoke(context);
    }

        private void OnRoomMuteChanged(Guid roomId)
    {
        if (_currentRoomId != roomId) return;
        IsMuted = _flags.GetMuted(roomId);
        NotifyChanged();
    }

    private bool _blockedByMe;
    private bool _blockedMe;

    private void WireBlockEvents()
    {
        _flags.BlockedByMeChanged += OnBlockedByMeChanged;
        _flags.BlockedMeChanged += OnBlockedMeChanged;
    }

    private void OnBlockedByMeChanged(Guid userId)
    {
        if (OtherUser?.Id != userId) return;

        var blocked = _flags.GetBlockedByMe(userId);
        IsBlockedByMe = blocked;

        if (blocked)
        {
            if (OtherUser != null)
            {
                OtherUser.IsOnline = false;
                OtherUser.LastSeen = null;
            }
        }
        else          {
                        _ = Task.Run(async () =>
            {
                try
                {
                    var onlineUsers = await _realtime.GetOnlineUsersAsync();
                    if (OtherUser != null)
                    {
                        OtherUser.IsOnline = onlineUsers.Contains(userId);

                        if (!OtherUser.IsOnline)
                        {
                            var status = await _realtime.GetUserOnlineStatus(userId);
                            if (status != null)
                            {
                                var lastSeen = (DateTime?)status.GetType().GetProperty("LastSeen")?.GetValue(status);
                                OtherUser.LastSeen = lastSeen;
                            }
                        }
                        else
                        {
                            OtherUser.LastSeen = null;
                        }
                        NotifyChanged("Unblocked via SignalR - Refreshed");
                    }
                }
                catch { }
            });
        }

        NotifyChanged();
    }
    private void OnBlockedMeChanged(Guid userId)
    {
        if (OtherUser?.Id != userId) return;

        var blocked = _flags.GetBlockedMe(userId);
        IsBlockedMe = blocked;

        if (blocked)
        {
            OtherUser.IsOnline = false;
            OtherUser.LastSeen = null;
        }
        else          {
            _ = Task.Run(async () =>
            {
                try
                {
                                        var onlineUsers = await _realtime.GetOnlineUsersAsync();
                    OtherUser.IsOnline = onlineUsers.Contains(userId);

                    if (OtherUser.IsOnline)
                    {
                        OtherUser.LastSeen = null;                      }
                    else
                    {
                                                try
                        {
                            var status = await _realtime.GetUserOnlineStatus(userId);
                            if (status != null)
                            {
                                var lastSeen = (DateTime?)status.GetType().GetProperty("LastSeen")?.GetValue(status);
                                OtherUser.LastSeen = lastSeen;
                            }
                        }
                        catch
                        {
                                                        OtherUser.LastSeen = null;                              Console.WriteLine("[Unblock fallback] GetUserOnlineStatus failed, using safe default");
                        }
                    }

                    NotifyChanged("Unblock - Status refreshed (fallback)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Unblock Refresh] Error: {ex.Message}");
                                        OtherUser.IsOnline = false;
                    OtherUser.LastSeen = null;
                    NotifyChanged();
                }
            });
        }

        NotifyChanged();
    }

        private void OnRealtimeRoomUpdated(RoomUpdatedModel upd)
    {
        Console.WriteLine($"[VM] 🟢 BEFORE: IsMuted={IsMuted}, AFTER: {upd.IsMuted}");

        if (_currentRoomId != upd.RoomId) return;

        Console.WriteLine($"[VM] RoomUpdated received: Room={upd.RoomId}, IsMuted={upd.IsMuted}");

        if (IsMuted != upd.IsMuted)
        {
            IsMuted = upd.IsMuted;
            _flags.SetMuted(upd.RoomId, upd.IsMuted);
            Console.WriteLine($"[VM] ✅ IsMuted updated to {upd.IsMuted}");
            NotifyChanged();
        }
    }        public async ValueTask DisposeAsync()
    {
        _flags.SetActiveRoom(null);
        _flags.RoomMuteChanged -= OnRoomMuteChanged;
        _flags.BlockedByMeChanged -= OnBlockedByMeChanged;
        _flags.BlockedMeChanged -= OnBlockedMeChanged;

        UnregisterRealtimeEvents();

        if (_currentRoomId != null)
        {
            await _realtime.LeaveRoomAsync(_currentRoomId.Value);
            Console.WriteLine($"[VM] Left room {_currentRoomId}");
        }

                OnlineUsers.Clear();
        TypingUsers.Clear();

                if (OtherUser != null)
        {
            OtherUser.IsOnline = false;
            OtherUser.LastSeen = null;
        }

        await _realtime.DisconnectAsync();         _currentRoomId = null;

        NotifyChanged();     }

        public void Dispose()
    {
                _flags.RoomMuteChanged -= OnRoomMuteChanged;
        _flags.BlockedByMeChanged -= OnBlockedByMeChanged;
        _flags.BlockedMeChanged -= OnBlockedMeChanged;
        UnregisterRealtimeEvents();
    }
    public void DebugMessages()
    {
        Console.WriteLine($"=== DEBUG MESSAGES ===");
        Console.WriteLine($"Total messages: {Messages.Count}");

        var original = Messages.Where(m => !m.ReplyToMessageId.HasValue).ToList();
        var replies = Messages.Where(m => m.ReplyToMessageId.HasValue).ToList();

        Console.WriteLine($"Original: {original.Count}, Replies: {replies.Count}");

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
            msg.UpdatedAt = DateTime.UtcNow;

                        msg.PersonalStatus = ClientMessageStatus.Delivered;
            msg.IsConfirmedRead = false;
            msg.ReadCount = 0;

            NotifyChanged();
        }
    }
    private void OnMessageDeleted(Guid messageId, bool isForEveryone)
    {
        if (isForEveryone)
        {
                        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
            {
                msg.IsDeleted = true;
                msg.Content = "🚫 This message was deleted";
                NotifyChanged();
            }
        }
        else
        {
                        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
            {
                Messages.Remove(msg);
                NotifyChanged();
            }
        }
    }
    public async Task EditMessageAsync(Guid messageId, string newContent)
    {
        newContent = newContent.Trim();
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);

                var savedReadCount = msg?.ReadCount ?? 0;
        var savedDeliveredCount = msg?.DeliveredCount ?? 0;

        if (msg != null)
        {
            msg.IsBeingEdited = true;
            msg.Content = newContent;
            msg.IsEdited = true;
            msg.UpdatedAt = DateTime.UtcNow;
            NotifyChanged();
        }

        try
        {
            await _chatService.EditMessageAsync(messageId, newContent);

            if (msg != null)
            {
                msg.IsBeingEdited = false;
                msg.UpdatedAt = DateTime.UtcNow;
                                NotifyChanged();
            }
        }
        catch (Exception ex)
        {
            if (msg != null)
            {
                msg.Content = msg.Content;                 msg.IsBeingEdited = false;
                NotifyChanged();
            }
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
                    }

    public void AddOrUpdateMessage(MessageModel message)
    {
        var existing = Messages.FirstOrDefault(m => m.Id == message.Id);
        if (existing == null)
        {
            existing = Messages.Where(m => m.SenderId == message.SenderId)
                .Where(m => m.Status == ClientMessageStatus.Pending || m.Status == ClientMessageStatus.Sent)
                .Where(m => string.Equals(m.Content, message.Content, StringComparison.Ordinal))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault(m => Math.Abs((m.CreatedAt - message.CreatedAt).TotalSeconds) <= 15);
        }

        if (existing != null)
        {
            existing.Id = message.Id;
            if (message.ReplyInfo != null)
                existing.ReplyInfo = message.ReplyInfo;
            existing.IsSystemMessage = message.IsSystemMessage;
            if (message.ReplyToMessageId.HasValue)
                existing.ReplyToMessageId = message.ReplyToMessageId;
            existing.DeliveredCount = Math.Max(existing.DeliveredCount, message.DeliveredCount);
            existing.ReadCount = Math.Max(existing.ReadCount, message.ReadCount);

            int roomMemberCount = Room?.Type == "Group" ? (GroupMembers?.Members.Count ?? 1) - 1 : 1;
            existing.TotalRecipients = Math.Max(roomMemberCount, message.TotalRecipients);

                        if (Room?.Type == "Private")
            {
                if (message.ShouldForceRead || existing.ShouldForceRead)
                {
                    existing.PersonalStatus = ClientMessageStatus.Read;
                    existing.ShouldForceRead = true;
                }
                else existing.PersonalStatus = (ClientMessageStatus)Math.Max((int)existing.PersonalStatus, (int)message.PersonalStatus);
            }
            else
            {
                                if (existing.ReadCount >= existing.TotalRecipients && existing.TotalRecipients > 0)
                    existing.PersonalStatus = ClientMessageStatus.Read;
                else if (existing.DeliveredCount >= 1)
                    existing.PersonalStatus = ClientMessageStatus.Delivered;
            }

            ApplyPendingStatsIfAny(existing.Id);
            NotifyChangedThrottled();
            return;
        }

        if (Room?.Type == "Private" && message.ShouldForceRead) message.PersonalStatus = ClientMessageStatus.Read;
        Messages.Add(message);
        ApplyPendingStatsIfAny(message.Id);
        NotifyChangedThrottled();
    }
    private void ApplyPendingStatsIfAny(Guid messageId)
    {
        if (_pendingStats.TryGetValue(messageId, out var s))
        {
            var msg = Messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
                ApplyStats(msg, s.total, s.delivered, s.read);

            _pendingStats.Remove(messageId);
        }
    }


    private void OnMessageReceiptStatsUpdated(Guid messageId, Guid roomId, int total, int delivered, int read)
    {
        if (roomId != _currentRoomId) return;

        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        Console.WriteLine($"[VM] Stats apply? found={(msg != null)} id={messageId}");

        if (msg == null)
        {
            _pendingStats[messageId] = (total, delivered, read);
            return;
        }

        ApplyStats(msg, total, delivered, read);
    }

            private void ApplyStats(MessageModel msg, int total, int delivered, int read)
    {
        int roomMemberCount = Room?.Type == "Group"
            ? (GroupMembers?.Members.Count ?? 1) - 1
            : 1;

        msg.TotalRecipients = total > 0 ? total : Math.Max(msg.TotalRecipients, roomMemberCount);
        msg.DeliveredCount = delivered;
        msg.ReadCount = read;

        if (msg.SenderId == CurrentUserId)
        {
            ClientMessageStatus newStatus;

            if (Room?.Type == "Private")
            {
                if (read >= 1)
                    newStatus = ClientMessageStatus.Read;                        else if (delivered >= 1)
                    newStatus = ClientMessageStatus.Delivered;                   else
                    newStatus = ClientMessageStatus.Sent;                    }
            else
            {
                if (read >= total && total > 0)
                    newStatus = ClientMessageStatus.Read;
                else if (delivered >= 1)
                    newStatus = ClientMessageStatus.Delivered;
                else
                    newStatus = ClientMessageStatus.Sent;
            }

            msg.PersonalStatus = newStatus;
            Console.WriteLine($"[ApplyStats] status={newStatus}, read={read}, delivered={delivered}, IsPageActive={IsPageActive}");
        }

        NotifyChangedThrottled();
    }
    private void OnMessageStatusUpdated(Guid messageId, Guid userId, int statusInt)
    {
        Console.WriteLine($"[VM] 🔵 OnMessageStatusUpdated ENTERED: msg={messageId}, userId={userId}, status={statusInt}");
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);

        if (msg == null)
        {
            Console.WriteLine($"[VM] Message not found!");
            return;
        }

        var newStatus = (ClientMessageStatus)statusInt;
        Console.WriteLine($"[VM] Old status: {msg.PersonalStatus}, New status: {newStatus}");

        if (msg.SenderId == CurrentUserId || userId == CurrentUserId)
        {
            if (newStatus > msg.PersonalStatus)
            {
                                msg.PersonalStatus = newStatus;
                msg.ReadCount = Math.Max(msg.ReadCount, 1);
                Console.WriteLine($"[VM] ✅ Status updated to {newStatus}");

                
                                msg.NotifyPropertyChanged(nameof(MessageModel.PersonalStatus));

                                var index = Messages.IndexOf(msg);
                if (index >= 0)
                {
                    Messages[index] = msg;
                }

                                Changed?.Invoke();
            }
        }
    }
    public void DeactivateChat()
    {
                _flags.SetActiveRoom(null);

                _currentRoomId = null;

                IsPageActive = false;

                UnregisterRealtimeEvents();

        Console.WriteLine("[VM] Chat deactivated and ActiveRoom cleared.");
    }
    public async Task<MessageReactionsDetailsDto?> GetMessageReactionsDetailsAsync(Guid messageId)
    {
        try
        {
            return await _chatService.GetMessageReactionsDetailsAsync(messageId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VM] Error getting reaction details: {ex.Message}");
            return null;
        }
    }

    public async Task RemoveMyReactionAsync(Guid messageId)
    {
        try
        {
            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message?.Reactions?.CurrentUserReactionType == null) return;

            var reactionType = message.Reactions.CurrentUserReactionType.Value;
            await AddReactionAsync(messageId, reactionType);

            Console.WriteLine($"[VM] ✅ Removed reaction {reactionType} from message {messageId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VM] Error removing reaction: {ex.Message}");
            _toasts.Error("Error", "Failed to remove reaction");
        }
    }
    public async Task MarkMessagesAsReadOnExit()
    {
        if (_currentRoomId == null || !Messages.Any()) return;

        try
        {
                        var lastMessageFromOther = Messages
                .Where(m => m.SenderId != CurrentUserId)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            if (lastMessageFromOther != null)
            {
                Console.WriteLine($"[VM] Marking ALL messages as read on exit up to {lastMessageFromOther.Id}");

                                foreach (var msg in Messages.Where(m => m.SenderId != CurrentUserId))
                {
                    if (msg.PersonalStatus < ClientMessageStatus.Read)
                    {
                        msg.PersonalStatus = ClientMessageStatus.Read;
                        msg.ShouldForceRead = true;
                    }
                }

                                await MarkRoomReadAsync(_currentRoomId.Value, lastMessageFromOther.Id);

                                if (_roomsVM != null)
                {
                    _roomsVM.UpdateLastMessageStatus(_currentRoomId.Value, lastMessageFromOther.Id, ClientMessageStatus.Read);
                    _roomsVM.MarkRoomAsReadLocal(_currentRoomId.Value, lastMessageFromOther.Id);
                }

                NotifyChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VM] Error marking messages as read on exit: {ex.Message}");
        }
    }
        public async Task DeleteChatAsync(Guid roomId)
    {
        Console.WriteLine($"[VM] DeleteChat called with roomId={roomId}");

        try
        {
            await _chatService.DeleteChatAsync(roomId);
        }
        catch (Exception ex)
        {
            _toasts.Error("Delete failed", ex.Message);
            throw;
        }
    }

        public async Task ClearChatAsync(Guid roomId, bool forEveryone = false)
    {
        try
        {
            await _chatService.ClearChatAsync(roomId, forEveryone);
            Messages.Clear();
            NotifyChanged();
        }
        catch (Exception ex)
        {
            _toasts.Error("Clear failed", ex.Message);
            throw;
        }
    }
    private void OnChatCleared(Guid roomId, bool forEveryone)
    {
        if (_currentRoomId != roomId) return;

        Console.WriteLine($"[VM] 🧹 ChatCleared received: room={roomId}, forEveryone={forEveryone}");

        Messages.Clear();
        PinnedMessages.Clear();
        NotifyChanged();
    }

    public void SyncLastMessageToRoomsVM()
    {
        if (_currentRoomId == null || !Messages.Any()) return;

        var lastMessage = Messages
            .Where(m => m.SenderId != Guid.Empty && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();

        if (lastMessage == null) return;

                if (GroupMembers?.Members != null)
            foreach (var member in GroupMembers.Members)
                if (!string.IsNullOrEmpty(member.DisplayName))
                    _roomsVM.CacheMemberName(member.Id, member.DisplayName);

                _roomsVM.EnrichRoomMemberNames(_currentRoomId.Value, GroupMembers?.Members);

        var status = (EnterpriseChat.Client.Models.MessageStatus)(int)lastMessage.PersonalStatus;
        _roomsVM.UpdateRoomPreview(
            _currentRoomId.Value,
            lastMessage.Id,
            lastMessage.Content,
            lastMessage.SenderId,
            lastMessage.CreatedAt,
            status
        );
    }
    private void OnRealtimeMessageReceived(MessageModel message)
    {
        if (IsBlockedByMe && message.SenderId != CurrentUserId)
            return;

        if (_currentRoomId == message.RoomId)
        {
            if (Room?.Type == "Private" && message.SenderId != CurrentUserId && IsPageActive)
            {
                message.PersonalStatus = ClientMessageStatus.Read;
                message.IsConfirmedRead = true;
                _roomsVM?.UpdateLastMessageStatus(_currentRoomId.Value, message.Id, ClientMessageStatus.Read);
            }

            AddOrUpdateMessage(message);

            if (_roomsVM != null && message.RoomId == _currentRoomId)
            {
                bool isMuted = _flags.GetMuted(message.RoomId);

                                string? senderName = message.SenderId == CurrentUserId
                    ? null                      : GetSenderName(message.SenderId);

                _roomsVM.UpdateRoomPreview(
                    roomId: message.RoomId,
                    messageId: message.Id,
                    preview: message.Content,
                    senderId: message.SenderId,
                    messageAt: message.CreatedAt,
                    status: message.PersonalStatus,
                    skipUnread: isMuted,
                    skipNotify: isMuted,
                    senderName: senderName                  );
            }

            if (message.SenderId != CurrentUserId && _currentRoomId != null && IsPageActive)
            {
                var lastOther = Messages
                    .Where(m => m.SenderId != CurrentUserId)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();

                if (lastOther != null)
                {
                    _ = Task.Run(async () => {
                        try
                        {
                            await Task.Delay(400);
                            await MarkRoomReadAsync(_currentRoomId.Value, lastOther.Id);
                        }
                        catch { }
                    });
                }
            }
        }
    }
}
