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
    private readonly RoomsViewModel _roomsVM;
    // ✅ الأحداث - تعريف واحد فقط لكل حدث
    public event Action? Changed;
    public event Func<MessageModel, Task>? MessageReceived;
    public event Func<MessageModel, Task>? MessageUpdated;
    public event Func<MessageModel, Task>? MessageReplyReceived;
    public event Action<ReplyContext?>? ReplyContextChanged;
    private Guid? _openMenuMessageId;

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
        ModerationApi mod, ReactionsApi reactionsApi,
         RoomsViewModel roomsVM)
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
    public bool IsBlockedByMe { get;  set; }
    public bool IsBlockedMe { get;  set; }
    public bool IsDisconnected { get; private set; }
    public bool IsOtherDeleted { get; private set; }
    public bool IsRemoved { get; private set; }
    private string _searchQuery = string.Empty;
    public string SearchQuery { get => _searchQuery; set { _searchQuery = value; NotifyChanged(); _ = PerformSearchAsync(); } }
    public bool IsPageActive { get; set; } // خاصية جديدة لتحديد هل المستخدم يشاهد الشات حالياً
    private bool _isSearching;
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

    // ✅ InitializeAsync
    // ✅ InitializeAsync
    public async Task InitializeAsync(Guid roomId)
    {
        // 1. تنظيف الحالة السابقة
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

        _currentRoomId = roomId;
        _flags.SetActiveRoom(roomId);
        NotifyChanged();

        try
        {
            // 2. التحقق من هوية المستخدم
            CurrentUserId = await _currentUser.GetUserIdAsync()
                ?? throw new InvalidOperationException("User not authenticated");

            // 3. جلب بيانات الغرفة
            Room = await _roomService.GetRoomAsync(roomId);
            if (Room == null)
            {
                UiError = "This room no longer exists.";
                NotifyChanged();
                return;
            }

            // 4. تحميل الرسائل
            var rawMessages = await _chatService.GetMessagesAsync(roomId, CurrentUserId, 0, 200);
            var uniqueMessages = rawMessages
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .OrderBy(m => m.CreatedAt)
                .ToList();

            // 5. معالجة بيانات الأعضاء (تتم قبل إضافة الرسائل لحساب العداد بدقة)
            if (Room.Type == "Group")
            {
                var dto = await _chatService.GetGroupMembersAsync(roomId);
                GroupMembers = new GroupMembersModel
                {
                    OwnerId = dto.OwnerId,
                    Members = dto.Members.Select(m => new UserModel
                    {
                        Id = m.Id,
                        DisplayName = m.DisplayName ?? "User"
                    }).ToList()
                };
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

            // ✅ 6. تحديث عدد المستلمين لكل رسالة (هذا يحل مشكلة الصح الواحدة بعد الريفرش)
            int memberCount = Room.Type == "Group" ? (GroupMembers?.Members.Count ?? 1) - 1 : 1;

            foreach (var msg in uniqueMessages)
            {
                msg.TotalRecipients = memberCount;
                Messages.Add(msg);
            }

            // 7. بقية الإجراءات (المثبتات، التنبيهات، الربط)
            var pinnedMsg = Messages.FirstOrDefault(m => m.Id == Room.PinnedMessageId);
            if (pinnedMsg != null) PinnedMessages.Add(pinnedMsg);

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
    // ميثود مساعدة ضيفها في الـ ChatViewModel من تحت
    private void SafeAddOrUpdateMessage(MessageModel newMessage)
    {
        // 1. ابحث لو الرسالة موجودة فعلاً بالـ ID الحقيقي
        var existing = Messages.FirstOrDefault(m => m.Id == newMessage.Id);

        // 2. لو مش موجودة بالـ ID، ابحث لو فيه نسخة Pending بنفس المحتوى
        if (existing == null)
        {
            existing = Messages.FirstOrDefault(m =>
                m.Status == ClientMessageStatus.Pending &&
                m.Content == newMessage.Content &&
                m.SenderId == newMessage.SenderId);
        }

        if (existing != null)
        {
            // تحديث النسخة الموجودة بدل إضافة واحدة جديدة
            existing.Id = newMessage.Id;
            existing.Status = newMessage.Status;
            existing.PersonalStatus = newMessage.PersonalStatus;
            existing.ReplyInfo = newMessage.ReplyInfo;
            // أي خصائص تانية محتاج تحدثها...
        }
        else
        {
            // إضافة فقط لو مش موجودة نهائياً
            Messages.Add(newMessage);
        }
    }

    // ✅ Register/Unregister Realtime Events
    private void RegisterRealtimeEvents(Guid roomId)
    {
        if (_eventsRegistered && _eventsRoomId == roomId)
            return;

        UnregisterRealtimeEvents();
        _eventsRoomId = roomId;
        Console.WriteLine($"[VM] Registering realtime events for room {roomId}"); // ✅
        _realtime.MessageReceived += OnRealtimeMessageReceived;
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
     
        _realtime.MessageReactionUpdated += OnMessageReactionUpdated;
        _realtime.MessageUpdated += OnMessageUpdated;
        _realtime.MessageDeleted += OnMessageDeleted;
        _realtime.MessagePinned += OnMessagePinned;
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
        _eventsRegistered = false;
        _eventsRoomId = null;
    }

    // ✅ Realtime Event Handlers
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
            // سحب قائمة الأونلاين الحالية من السيرفر
            var onlineUsers = await _realtime.GetOnlineUsersAsync();

            if (onlineUsers.Contains(userId))
            {
                // نستخدم NotifyChanged بدلاً من InvokeAsync
                // لأنها ستقوم بتنبيه صفحة Chat.razor لتعمل StateHasChanged
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

        // 1. Immediate removal from OnlineUsers collection
        var userToRemove = OnlineUsers.FirstOrDefault(u => u.Id == id);
        if (userToRemove != null)
        {
            OnlineUsers.Remove(userToRemove);
            Console.WriteLine($"[VM] Removed user {id} from OnlineUsers. New count: {OnlineUsers.Count}");
        }

        // 2. Update OtherUser if this is a private chat
        if (OtherUser?.Id == id)
        {
            OtherUser.IsOnline = false;
            OtherUser.LastSeen = DateTime.UtcNow;
        }

        // 3. Remove from typing users if they were typing
        var typingUser = TypingUsers.FirstOrDefault(u => u.Id == id);
        if (typingUser != null)
        {
            TypingUsers.Remove(typingUser);
        }

        // 4. CRITICAL: Force UI update immediately - use NOTIFYCHANGED not throttled
        NotifyChanged(); // ← مهم: استخدم NotifyChanged وليس NotifyChangedThrottled

        // 5. Also force rebuild for group
        if (Room?.Type == "Group")
        {
            // Don't do async - rebuild immediately
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

            // rebuild فوري
            RebuildPresenceFromRealtime();

            NotifyChanged();
        }
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

        // ✅ Clear UI lists immediately
        OnlineUsers.Clear();
        TypingUsers.Clear();

        if (OtherUser != null)
        {
            OtherUser.IsOnline = false;
            OtherUser.LastSeen = DateTime.UtcNow;
        }

        // Force immediate UI update
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

        // تحديث البيانات بهدوء
        await RefreshRoomStateAsync(roomId);

        // ✅ إخطار كل المشتركين (بما فيهم الـ Sidebar) إن فيه تغيير حصل
        NotifyChanged("OnGroupRenamed");
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
        if (GroupMembers is null)
        {
            Console.WriteLine("[Rebuild] GroupMembers is null, skipping");
            return;
        }

        var onlineSet = _realtime.State.OnlineUsers.ToHashSet();
        Console.WriteLine($"[Rebuild] Raw online users from SignalR ({_realtime.State.OnlineUsers.Count}): {string.Join(", ", _realtime.State.OnlineUsers)}");

        // Don't clear and rebuild if counts are the same (optimization)
        // لكن للتأكد، سنقوم بالتحديث دائماً لهذه المشكلة
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

        // Clean up TypingUsers
        for (int i = TypingUsers.Count - 1; i >= 0; i--)
        {
            if (!onlineSet.Contains(TypingUsers[i].Id))
            {
                Console.WriteLine($"[Rebuild] Removed typing user {TypingUsers[i].Id} - no longer online");
                TypingUsers.RemoveAt(i);
            }
        }

        Console.WriteLine($"[Rebuild] Final result: {added} online users");

        // Force UI update
        NotifyChanged(); // ← تأكد من وجود هذه السطر
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

        // ✅ حساب عدد المستلمين فوراً لضمان دقة حالة الجروب
        int currentMemberCount = Room?.Type == "Group" ? (GroupMembers?.Members.Count ?? 1) - 1 : 1;

        var tempId = Guid.NewGuid();
        var pending = new MessageModel
        {
            Id = tempId,
            Content = text,
            Status = ClientMessageStatus.Pending,
            SenderId = CurrentUserId,
            CreatedAt = DateTime.UtcNow,
            TotalRecipients = currentMemberCount // 🚀 تحديد العدد المستهدف فوراً
        };

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
    private void OnMessageStatusChanged(Guid messageId, string status)
    {
        // 1. البحث عن الرسالة في القائمة
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            // 2. تحويل الـ string اللي جاي من SignalR لـ Enum الحالة بتاعك
            if (Enum.TryParse<ClientMessageStatus>(status, true, out var newStatus))
            {
                msg.Status = newStatus;

                // لو الشخص التاني هو اللي قرأ، نحدث الـ PersonalStatus برضه عشان تظهر الزرقاء
                msg.PersonalStatus = newStatus;

                // 3. أهم خطوة: إجبار Blazor على إعادة الرسم
                NotifyChanged();
            }
        }
    }

    // ✅ SendMessageWithReplyAsync - واحدة فقط بدون تكرار
    public async Task SendMessageWithReplyAsync(Guid roomId, string text, Guid? replyToMessageId, ReplyInfoModel? replySnapshot)
    {
        Console.WriteLine($"[VM] SendMessageWithReplyAsync room={roomId} text='{text}' replyTo={replyToMessageId}");

        if (IsMuted) { _toasts.Warning("Muted", "This chat is muted."); return; }
        if (IsBlockedByMe) { _toasts.Warning("Blocked", "You blocked this user."); return; }
        if (IsOtherDeleted) { _toasts.Warning("Unavailable", "This user is no longer available."); return; }
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 2000) { _toasts.Warning("Too long", "Message is too long."); return; }

        // ✅ حساب عدد المستلمين فوراً
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
            TotalRecipients = currentMemberCount // 🚀 تحديد العدد المستهدف فوراً
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

                if (replyToMessageId.HasValue && _realtime is ChatRealtimeClient realtimeClient)
                {
                    await realtimeClient.SendMessageWithReplyAsync(roomId, pending);
                }

                if (MessageReplyReceived != null) await MessageReplyReceived.Invoke(pending);
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
            // 1. حاول عبر SignalR (أسرع)
            await _realtime.MarkRoomReadAsync(roomId, lastMessageId);
            Console.WriteLine($"[VM] MarkRoomRead via SignalR successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VM] SignalR MarkRoomRead failed: {ex.Message}");
            try
            {
                // 2. Fallback للـ HTTP API
                await _chatService.MarkRoomReadAsync(roomId, lastMessageId);
                Console.WriteLine($"[VM] MarkRoomRead via HTTP successful");
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"[VM] HTTP MarkRoomRead failed: {ex2.Message}");
            }
        }

        // 3. تحديث محلي فوري (بدون انتظار السيرفر)
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
            IsBlockedByMe = true;                    // ← التغيير
            _flags.SetBlockedByMe(userId, true);     // ← التغيير (مش SetBlocked)
            TypingUsers.Clear();
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
                        OtherUser.LastSeen = null;  // fallback آمن
                    }
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
            var currentlyMuted = _flags.GetMuted(roomId);
            var nextMuted = !currentlyMuted;

            if (nextMuted)
                await _chatService.MuteAsync(roomId);  // ✅ API + DB
            else
                await _chatService.UnmuteAsync(roomId); // ✅ API + DB

            _flags.SetMuted(roomId, nextMuted);
            IsMuted = nextMuted;

            NotifyChanged();
        }
        catch (Exception ex)
        {
            _toasts.Error("Failed", $"Could not toggle mute: {ex.Message}");
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
        else  // ← Unblock via SignalR
        {
            // نفس المنطق: refresh الحالة
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
        else  // Unblock
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // أول حاجة: جيب الأونلاين (ده آمن ومش بيفشل عادة)
                    var onlineUsers = await _realtime.GetOnlineUsersAsync();
                    OtherUser.IsOnline = onlineUsers.Contains(userId);

                    if (OtherUser.IsOnline)
                    {
                        OtherUser.LastSeen = null;  // مهم جدًا: نصفر LastSeen لو أونلاين
                    }
                    else
                    {
                        // لو مش أونلاين، حاول جيب LastSeen، ولو فشل نتركه null أو قيمة افتراضية
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
                            // fallback: لو فشل GetUserOnlineStatus، نستخدم قيمة آمنة
                            OtherUser.LastSeen = null;  // أو DateTime.UtcNow لو عايز "Offline" بدون وقت
                            Console.WriteLine("[Unblock fallback] GetUserOnlineStatus failed, using safe default");
                        }
                    }

                    NotifyChanged("Unblock - Status refreshed (fallback)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Unblock Refresh] Error: {ex.Message}");
                    // أسوأ حالة: نفرض Offline بدون LastSeen
                    OtherUser.IsOnline = false;
                    OtherUser.LastSeen = null;
                    NotifyChanged();
                }
            });
        }

        NotifyChanged();
    }

    // ✅ DisposeAsync - واحدة فقط
    public async ValueTask DisposeAsync()
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

        // ← أضف ده: صفر القوائم عشان العدد ينقص فورًا لما تخرج
        OnlineUsers.Clear();
        TypingUsers.Clear();

        // ← أضف ده كمان: صفر الـ OtherUser لو شات خاص
        if (OtherUser != null)
        {
            OtherUser.IsOnline = false;
            OtherUser.LastSeen = null;
        }

        await _realtime.DisconnectAsync(); // لو عايز تقطع الاتصال كامل (اختياري)
        _currentRoomId = null;

        NotifyChanged(); // عشان الـ UI يرندر العدد 0 فورًا
    }

    // ✅ IDisposable implementation
    public void Dispose()
    {
        // Sync cleanup if needed
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
            existing.DeliveredCount = Math.Max(existing.DeliveredCount, message.DeliveredCount);
            existing.ReadCount = Math.Max(existing.ReadCount, message.ReadCount);

            int roomMemberCount = Room?.Type == "Group" ? (GroupMembers?.Members.Count ?? 1) - 1 : 1;
            existing.TotalRecipients = Math.Max(roomMemberCount, message.TotalRecipients);

            // ✅ إعادة تقييم الحالة فوراً (هذا ما يحل مشكلة الريفرش)
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
                // في الجروب: تحول للأزرق فقط لو العدد اكتمل الآن
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

    // 1. أضف هذه الدالة المساعدة في نهاية ChatViewModel.cs
    private void MarkAllPreviousAsRead(MessageModel currentMsg)
    {
        var index = Messages.IndexOf(currentMsg);
        if (index <= 0) return;

        for (int i = 0; i < index; i++)
        {
            var prevMsg = Messages[i];
            // إذا كانت الرسالة مني ولم تكن زرقاء بعد، اجعلها زرقاء
            if (prevMsg.SenderId == CurrentUserId && prevMsg.PersonalStatus < ClientMessageStatus.Read)
            {
                prevMsg.PersonalStatus = ClientMessageStatus.Read;
                prevMsg.ReadCount = Math.Max(prevMsg.ReadCount, 1);
            }
        }
    }
    // 2. استدعها داخل دالة ApplyStats (التي عدلناها سابقاً)
    private void ApplyStats(MessageModel msg, int total, int delivered, int read)
    {
        int roomMemberCount = Room?.Type == "Group" ? (GroupMembers?.Members.Count ?? 1) - 1 : 1;
        msg.TotalRecipients = total > 0 ? total : Math.Max(msg.TotalRecipients, roomMemberCount);
        msg.DeliveredCount = Math.Max(msg.DeliveredCount, delivered);
        msg.ReadCount = Math.Max(msg.ReadCount, read);

        if (msg.SenderId == CurrentUserId)
        {
            ClientMessageStatus newStatus;
            if (msg.ReadCount >= msg.TotalRecipients && msg.TotalRecipients > 0)
            {
                newStatus = ClientMessageStatus.Read;
            }
            else if (msg.DeliveredCount >= 1)
            {
                newStatus = ClientMessageStatus.Delivered;
            }
            else
            {
                newStatus = ClientMessageStatus.Sent;
            }

            if (newStatus != msg.PersonalStatus)
            {
                msg.PersonalStatus = newStatus;
                // ← شيلنا الـ MarkAllPreviousAsRead تمامًا
            }
        }
        NotifyChangedThrottled();
    }
    private void OnMessageStatusUpdated(Guid messageId, Guid userId, int statusInt)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg == null) return;
        var newStatus = (ClientMessageStatus)statusInt;

        if (Room?.Type == "Group" && msg.SenderId == CurrentUserId)
        {
            msg.Status = (ClientMessageStatus)Math.Max((int)msg.Status, (int)newStatus);
            NotifyChangedThrottled();
            return;
        }

        if (msg.SenderId == CurrentUserId || userId == CurrentUserId)
        {
            if (newStatus > msg.PersonalStatus)
            {
                msg.PersonalStatus = newStatus;
                msg.ReadCount = Math.Max(msg.ReadCount, 1);
                // ← شيلنا الـ MarkAllPreviousAsRead
                NotifyChangedThrottled();
            }
        }
    }
    public void DeactivateChat()
    {
        // 1. تصفير الغرفة النشطة في المخزن لكي يبدأ العداد بالعمل مجدداً
        _flags.SetActiveRoom(null);

        // 2. تصفير المعرف المحلي
        _currentRoomId = null;

        // 3. إيقاف حالة النشاط
        IsPageActive = false;

        // 4. إلغاء الاشتراك من الأحداث لمنع استهلاك الذاكرة
        UnregisterRealtimeEvents();

        Console.WriteLine("[VM] Chat deactivated and ActiveRoom cleared.");
    }
    public async Task MarkMessagesAsReadOnExit()
    {
        if (_currentRoomId == null || !Messages.Any()) return;

        try
        {
            // نجيب آخر رسالة مش مني
            var lastMessageFromOther = Messages
                .Where(m => m.SenderId != CurrentUserId)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            if (lastMessageFromOther != null)
            {
                Console.WriteLine($"[VM] Marking ALL messages as read on exit up to {lastMessageFromOther.Id}");

                // نحدث الحالة محلياً لكل الرسائل
                foreach (var msg in Messages.Where(m => m.SenderId != CurrentUserId))
                {
                    if (msg.PersonalStatus < ClientMessageStatus.Read)
                    {
                        msg.PersonalStatus = ClientMessageStatus.Read;
                        msg.ShouldForceRead = true;
                    }
                }

                // نحدث السيرفر بآخر رسالة
                await MarkRoomReadAsync(_currentRoomId.Value, lastMessageFromOther.Id);

                // نحدث الـ Sidebar
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
    // واستخدمها بدل SafeAddOrUpdateMessage
    // في ChatViewModel.cs - عدل هذه الدالة
    private void OnRealtimeMessageReceived(MessageModel message)
    {
        // 🛡️ حماية الحظر: لو الشخص ده أنا عامله بلوك، ارمي الرسالة فوراً ولا تبعت Delivered Receipt
        if (IsBlockedByMe && message.SenderId != CurrentUserId)
        {
            Console.WriteLine($"[VM] Blocked message from {message.SenderId} ignored.");
            return;
        }

        if (_currentRoomId == message.RoomId)
        {
            // ✅ منطق القراءة الفورية للشات الخاص فقط
            if (Room?.Type == "Private" && message.SenderId != CurrentUserId && IsPageActive)
            {
                message.PersonalStatus = ClientMessageStatus.Read;
                message.ShouldForceRead = true;
                _roomsVM?.UpdateLastMessageStatus(_currentRoomId.Value, message.Id, ClientMessageStatus.Read);
            }

            AddOrUpdateMessage(message);

            // إشعار القراءة للسيرفر
            if (message.SenderId != CurrentUserId && _currentRoomId != null && IsPageActive)
            {
                var lastOther = Messages.Where(m => m.SenderId != CurrentUserId).OrderByDescending(m => m.CreatedAt).FirstOrDefault();
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
