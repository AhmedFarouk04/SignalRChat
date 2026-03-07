using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Components.Rooms;
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
    public string? CurrentUserName { get; private set; }
    private readonly Dictionary<Guid, string> _knownMemberNames = new();
    public string? LastReactionPreview { get; set; }
    private readonly Dictionary<Guid, bool> _typingStatus = new();
    private static readonly Guid SystemUserId = Guid.Empty;
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
        _ = InitializeUserInfoAsync();
        _flags.RoomUnreadChanged += OnRoomUnreadChanged;
        _flags.ActiveRoomChanged += OnActiveRoomChanged;
        _rt.TypingStarted += OnTypingStarted;
        _rt.TypingStopped += OnTypingStopped;
        _rt.GroupRenamed += OnGroupRenamed;
        _rt.RoomUpserted += OnRoomUpserted;
        _rt.MemberRemoved += (roomId, userId, removerName) =>
        _rt.MessageReceived += OnMessageReceived;
        _rt.RemovedFromRoom += OnRemovedFromRoom;
        _rt.RoomUpdated += OnRoomUpdated;
        _rt.MessageReactionUpdated += OnMessageReactionUpdated;
        _rt.MessageReceiptStatsUpdated += OnMessageReceiptStatsUpdated;
        _rt.RoomMuteChanged += OnRoomMuteChanged;
        _rt.MessageDeleted += OnMessageDeleted;

    }
    private async Task InitializeUserInfoAsync()
    {
        var userId = await _currentUser.GetUserIdAsync();
        if (userId.HasValue)
        {
            _cachedUserId = userId.Value;
            CurrentUserId = userId.Value;

                        CurrentUserName = await _currentUser.GetDisplayNameAsync();

            Console.WriteLine($"[RoomsVM] System Identify: {CurrentUserName}");

                        NotifyChanged();
        }
    }
    private void OnMessageDeleted(Guid messageId, bool isForEveryone)
    {
        var list = Rooms.ToList();

        var idx = list.FindIndex(r => r.LastMessageId == messageId);
        if (idx < 0) return;

        var room = list[idx];

        if (!isForEveryone && room.LastMessageSenderId != CurrentUserId) return;

        _flags.SetLastNonSystemPreview(room.Id, "🚫 This message was deleted");
        _flags.SetLastReactionPreview(room.Id, null);

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
            LastMessagePreview = "🚫 This message was deleted",
            LastMessageId = room.LastMessageId,
            LastMessageSenderId = room.LastMessageSenderId,
            LastMessageStatus = null,
            MemberNames = room.MemberNames
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }
    private void OnRoomMuteChanged(Guid roomId, bool isMuted)
    {
        Console.WriteLine($"[RoomsVM] 🔕 RoomMuteChanged: Room={roomId}, IsMuted={isMuted}");

                _flags.SetMuted(roomId, isMuted);

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        var room = list[idx];
        if (room.IsMuted == isMuted) return;

        list[idx] = new RoomListItemModel
        {
            Id = room.Id,
            Name = room.Name,
            Type = room.Type,
            OtherUserId = room.OtherUserId,
            OtherDisplayName = room.OtherDisplayName,
            IsMuted = isMuted,
            UnreadCount = room.UnreadCount,
            LastMessageAt = room.LastMessageAt,
            LastMessagePreview = room.LastMessagePreview,
            LastMessageId = room.LastMessageId,
            LastMessageSenderId = room.LastMessageSenderId,
            LastMessageStatus = room.LastMessageStatus,
            MemberNames = room.MemberNames,
            IsTyping = room.IsTyping
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }
    private void OnMessageReactionUpdated(Guid messageId, Guid reactorId, int reactionTypeInt, bool isNewReaction)
    {
                    }
    private void OnRemovedFromRoom(Guid roomId)
    {
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        var room = list[idx];

        list[idx] = new RoomListItemModel
        {
            Id = room.Id,
            Name = room.Name,
            Type = room.Type,
            OtherUserId = room.OtherUserId,
            OtherDisplayName = room.OtherDisplayName,
            IsMuted = room.IsMuted,
            UnreadCount = 0, 
            LastMessageAt = DateTime.UtcNow,
            LastMessagePreview = "You were removed from this group", 
            LastMessageId = Guid.NewGuid(),
            LastMessageSenderId = Guid.Empty, 
            LastMessageStatus = null,
            MemberNames = room.MemberNames
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }

    private void OnMessageReceiptStatsUpdated(Guid messageId, Guid roomId, int total, int delivered, int read)
    {
        Console.WriteLine($"[RoomsVM] StatsUpdated for room {roomId}, msg {messageId}, d={delivered}, r={read}");

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        var room = list[idx];

                if (room.LastMessageId != messageId) return;

                var newStatus =
            (read >= total && total > 0) ? MessageStatus.Read :
            (delivered >= 1) ? MessageStatus.Delivered :
            MessageStatus.Sent;

                if (_lastMessageStatusCache.TryGetValue(roomId, out var cached) && cached.messageId == messageId)
        {
            if (cached.status > newStatus)
            {
                Console.WriteLine($"[RoomsVM] Using cached status {cached.status} instead of {newStatus}");
                newStatus = cached.status;
            }
        }

                if (room.LastMessageStatus.HasValue && newStatus < room.LastMessageStatus.Value)
        {
            Console.WriteLine($"[RoomsVM] Ignoring backward status update: {room.LastMessageStatus} -> {newStatus}");
            return;
        }

                if (room.LastMessageStatus != newStatus)
        {
            Console.WriteLine($"[RoomsVM] Updating last message status for room {roomId}: {room.LastMessageStatus} -> {newStatus}");

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
    }        public void UpdateLastMessageStatus(Guid roomId, Guid messageId, MessageStatus status)
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

                                if (room.LastMessageSenderId == CurrentUserId && status == MessageStatus.Read)
        {
            Console.WriteLine($"[RoomsVM] 🛡️ Blocked local 'Read' update for my own outgoing message!");
            return;
        }
        
                if (room.LastMessageId != messageId)
        {
            Console.WriteLine($"[RoomsVM] Message ID mismatch: room has {room.LastMessageId}, updating with {messageId}");
                    }

                if (room.LastMessageStatus.HasValue && status < room.LastMessageStatus.Value)
        {
            Console.WriteLine($"[RoomsVM] Ignoring manual downgrade: {room.LastMessageStatus} -> {status}");
            return;
        }

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
            LastMessageStatus = status          };

        Rooms = list;
        ApplyFilter();

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
    private void MergeWithExistingCache()
    {
        var list = Rooms.ToList();
        bool changed = false;

        for (int i = 0; i < list.Count; i++)
        {
            var room = list[i];

                        if (_lastMessageStatusCache.TryGetValue(room.Id, out var cached))
            {
                if (cached.messageId == room.LastMessageId &&
                    cached.status > (room.LastMessageStatus ?? MessageStatus.Sent))
                {
                    list[i] = new RoomListItemModel
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
                        LastMessageStatus = cached.status,
                        MemberNames = room.MemberNames
                    };
                    changed = true;
                }
            }
        }

        if (changed) Rooms = list;
    }
    public void ApplyCachedPreviews()
    {
        var list = Rooms.ToList();
        bool changed = false;

        for (int i = 0; i < list.Count; i++)
        {
            var room = list[i];

                        var cachedPreview = _flags.GetLastNonSystemPreview(room.Id);
            if (!string.IsNullOrEmpty(cachedPreview) &&
                string.IsNullOrEmpty(room.LastMessagePreview))
            {
                list[i] = new RoomListItemModel
                {
                    Id = room.Id,
                    Name = room.Name,
                    Type = room.Type,
                    OtherUserId = room.OtherUserId,
                    OtherDisplayName = room.OtherDisplayName,
                    IsMuted = room.IsMuted,
                    UnreadCount = room.UnreadCount,
                    LastMessageAt = room.LastMessageAt,
                    LastMessagePreview = cachedPreview,
                    LastMessageId = room.LastMessageId,
                    LastMessageSenderId = room.LastMessageSenderId,
                    LastMessageStatus = room.LastMessageStatus,
                    MemberNames = room.MemberNames
                };
                changed = true;
            }
        }

        if (changed)
        {
            Rooms = list;
            ApplyFilter();
            NotifyChanged();
        }
    }
    public void NotifyChangedPublic() => NotifyChanged();

    public async Task RefreshUnreadCountsAsync()
    {
        try
        {
            var freshRooms = await _roomService.GetRoomsAsync();
            var list = Rooms.ToList();
            bool changed = false;

            foreach (var fresh in freshRooms)
            {
                var idx = list.FindIndex(r => r.Id == fresh.Id);
                if (idx < 0) continue;

                var room = list[idx];

                                if (room.UnreadCount != fresh.UnreadCount)
                {
                    list[idx] = new RoomListItemModel
                    {
                        Id = room.Id,
                        Name = room.Name,
                        Type = room.Type,
                        OtherUserId = room.OtherUserId,
                        OtherDisplayName = room.OtherDisplayName,
                        IsMuted = fresh.IsMuted,
                        UnreadCount = fresh.UnreadCount,                          LastMessageAt = room.LastMessageAt,
                        LastMessagePreview = room.LastMessagePreview,                         LastMessageId = room.LastMessageId,
                        LastMessageSenderId = room.LastMessageSenderId,
                        LastMessageStatus = room.LastMessageStatus,
                        MemberNames = room.MemberNames
                    };
                    changed = true;
                }
            }

            if (changed)
            {
                Rooms = list;
                ApplyFilter();
                NotifyChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RoomsVM] RefreshUnreadCounts error: {ex.Message}");
        }
    }
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

                        foreach (var room in Rooms)
                foreach (var kvp in room.MemberNames)
                    _knownMemberNames[kvp.Key] = kvp.Value;

                        var enrichedList = Rooms.ToList();
            for (int i = 0; i < enrichedList.Count; i++)
            {
                var room = enrichedList[i];
                if (room.Type != "Group") continue;
                if (room.LastMessageSenderId == null || room.LastMessageSenderId == Guid.Empty) continue;

                var senderId = room.LastMessageSenderId.Value;

                if (!room.MemberNames.ContainsKey(senderId) && _knownMemberNames.TryGetValue(senderId, out var cachedName))
                {
                    var updatedNames = new Dictionary<Guid, string>(room.MemberNames)
                    {
                        [senderId] = cachedName
                    };

                    enrichedList[i] = new RoomListItemModel
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
                        MemberNames = updatedNames
                    };
                }
            }
            Rooms = enrichedList;

            MergeWithExistingCache();

            foreach (var r in Rooms)
            {
                _flags.SetUnread(r.Id, r.UnreadCount);

                var list = Rooms.ToList();
                var idx = list.FindIndex(x => x.Id == r.Id);
                if (idx < 0) continue;

                var room = list[idx];
                bool changed = false;

                if (_flags.GetRoomCleared(r.Id))
                {
                    Console.WriteLine($"[LoadAsync] Room {r.Id} was cleared, skipping cached preview");
                    _flags.SetLastNonSystemPreview(r.Id, null);
                    _flags.SetLastReactionPreview(r.Id, null);
                    _lastMessageStatusCache.Remove(r.Id);

                    list[idx] = new RoomListItemModel
                    {
                        Id = room.Id,
                        Name = room.Name,
                        Type = room.Type,
                        OtherUserId = room.OtherUserId,
                        OtherDisplayName = room.OtherDisplayName,
                        IsMuted = room.IsMuted,
                        UnreadCount = 0,
                        LastMessageAt = null,
                        LastMessagePreview = null,
                        LastMessageId = null,
                        LastMessageSenderId = null,
                        LastMessageStatus = null,
                        MemberNames = room.MemberNames
                    };
                    Rooms = list;
                    continue;
                }

                var savedStatus = _flags.GetLastMessageStatus(r.Id);
                if (savedStatus.HasValue && room.LastMessageStatus != savedStatus.Value)
                {
                    room.LastMessageStatus = savedStatus.Value;
                    changed = true;
                }

                var savedReactionPreview = _flags.GetLastReactionPreview(r.Id);
                if (!string.IsNullOrEmpty(savedReactionPreview))
                {
                    room.LastMessagePreview = savedReactionPreview;
                    changed = true;
                }
                else
                {
                    var savedPreview = _flags.GetLastNonSystemPreview(r.Id);
                    if (savedPreview != null && room.LastMessagePreview != savedPreview)
                    {
                        room.LastMessagePreview = savedPreview;
                        changed = true;
                    }
                }

                if (changed)
                {
                    list[idx] = room;
                    Rooms = list;
                }
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RoomsVM] 💥 FATAL ERROR IN LoadAsync: {ex.Message}");
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
    public void EnrichRoomMemberNames(Guid roomId, List<UserModel>? members)
    {
        if (members == null || !members.Any()) return;

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        var room = list[idx];

                var updatedNames = new Dictionary<Guid, string>(room.MemberNames ?? new());
        foreach (var member in members)
        {
            if (member.Id != CurrentUserId && !string.IsNullOrEmpty(member.DisplayName))
            {
                updatedNames[member.Id] = member.DisplayName;
                _knownMemberNames[member.Id] = member.DisplayName;             }
        }

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
            MemberNames = updatedNames
        };

        Rooms = list;
    }
    public async Task RefreshRoomStatusesAsync()
    {
        var freshRooms = await _roomService.GetRoomsAsync();
        var list = Rooms.ToList();

        foreach (var fresh in freshRooms)
        {
            var idx = list.FindIndex(r => r.Id == fresh.Id);
            if (idx < 0) continue;

            var room = list[idx];
            string? previewToUse;

                        var savedReactionPreview = _flags.GetLastReactionPreview(fresh.Id);
            if (!string.IsNullOrEmpty(savedReactionPreview))
            {
                previewToUse = savedReactionPreview;
            }
            else if (fresh.LastMessageSenderId == Guid.Empty)
            {
                                var savedPreview = _flags.GetLastNonSystemPreview(fresh.Id);
                previewToUse = savedPreview ?? fresh.LastMessagePreview;
            }
            else
            {
                                previewToUse = fresh.LastMessagePreview;
                _flags.SetLastNonSystemPreview(fresh.Id, fresh.LastMessagePreview);
            }

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
                LastMessagePreview = previewToUse,
                LastMessageId = fresh.LastMessageId,
                LastMessageSenderId = fresh.LastMessageSenderId,
                LastMessageStatus = fresh.LastMessageStatus,
                MemberNames = room.MemberNames
            };
        }

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
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
                                string? previewToUse = fresh.LastMessagePreview;

                                if (fresh.LastMessageSenderId == Guid.Empty)
                {
                    var savedPreview = _flags.GetLastNonSystemPreview(fresh.Id);
                    if (savedPreview != null)
                    {
                        previewToUse = savedPreview;
                        Console.WriteLine($"[RoomsVM] RefreshStatus: using saved preview '{savedPreview}' for system message");
                    }
                }
                else
                {
                                        _flags.SetLastNonSystemPreview(fresh.Id, fresh.LastMessagePreview);
                }

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
                    LastMessagePreview = previewToUse,                      LastMessageId = fresh.LastMessageId,
                    LastMessageSenderId = fresh.LastMessageSenderId,
                    LastMessageStatus = fresh.LastMessageStatus
                };
            }
        }

        Rooms = currentList;
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
        Console.WriteLine($"[UPSERT DEBUG] Id={dto.Id}, LastMsgId={dto.LastMessageId}, Preview='{dto.LastMessagePreview}'");

                if (dto.LastMessageId == null && string.IsNullOrEmpty(dto.LastMessagePreview))
        {
            var clearList = Rooms.ToList();
            var clearIdx = clearList.FindIndex(r => r.Id == dto.Id);
            if (clearIdx >= 0)
            {
                var cr = clearList[clearIdx];
                clearList[clearIdx] = new RoomListItemModel
                {
                    Id = cr.Id,
                    Name = cr.Name,
                    Type = cr.Type,
                    OtherUserId = cr.OtherUserId,
                    OtherDisplayName = cr.OtherDisplayName,
                    IsMuted = cr.IsMuted,
                    UnreadCount = 0,
                    MemberNames = cr.MemberNames                 };
                _flags.SetLastNonSystemPreview(dto.Id, null);
                _lastMessageStatusCache.Remove(dto.Id);
                Rooms = clearList;
                ApplyFilter();
                NotifyChanged();
            }
            return;
        }

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == dto.Id);

                        var existingMemberNames = (idx >= 0) ? list[idx].MemberNames : new Dictionary<Guid, string>();

        bool isSystemMessage = dto.LastMessageSenderId == Guid.Empty || dto.IsSystemMessage;
        string? finalPreview;

        if (isSystemMessage)
        {
            finalPreview = FormatSystemPreview(dto.LastMessagePreview);
            Console.WriteLine($"[RoomsVM] System message upsert: personalized preview = '{finalPreview}'");
        }
        else
        {
            finalPreview = dto.LastMessagePreview;
            _flags.SetLastNonSystemPreview(dto.Id, finalPreview);
        }

        bool isActive = _flags.ActiveRoomId == dto.Id;
        int finalUnreadCount = isActive ? 0 : dto.UnreadCount;

        MessageStatus? finalStatus;
        if (isActive && dto.LastMessageSenderId == _cachedUserId)
            finalStatus = MessageStatus.Sent;
        else
            finalStatus = dto.LastMessageStatus is null ? null : (MessageStatus?)(int)dto.LastMessageStatus.Value;

        var existingNames = (idx >= 0) ? list[idx].MemberNames : new Dictionary<Guid, string>();
        var model = new RoomListItemModel
        {
            Id = dto.Id,
            Name = dto.Name ?? "Room",
            Type = dto.Type ?? "Group",
            OtherUserId = dto.OtherUserId,
            OtherDisplayName = dto.OtherDisplayName,
            UnreadCount = finalUnreadCount,
            IsMuted = _flags.GetMuted(dto.Id) || dto.IsMuted,
            LastMessageAt = dto.LastMessageAt,
            LastMessagePreview = finalPreview,
            LastMessageId = dto.LastMessageId,
            LastMessageSenderId = dto.LastMessageSenderId,
            LastMessageStatus = finalStatus,

                        MemberNames = (dto.MemberNames != null && dto.MemberNames.Count > 0)
                  ? dto.MemberNames
                  : existingNames
        };

        if (idx >= 0)
            list[idx] = model;
        else
            list.Insert(0, model);

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }
    private string FormatSystemPreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

                var myName = CurrentUserName;
        if (string.IsNullOrEmpty(myName)) return text;

        var formatted = text;

                        if (formatted.StartsWith(myName, StringComparison.OrdinalIgnoreCase))
        {
                        formatted = "You" + formatted.Substring(myName.Length);
        }
                        else if (formatted.Contains(myName, StringComparison.OrdinalIgnoreCase))
        {
                        formatted = formatted.Replace(myName, "you", StringComparison.OrdinalIgnoreCase);
        }

                return formatted.Replace("  ", " ").Trim();
    }
    private bool _isFetchingMissingRoom = false;
    private async Task HandleMissingRoomAsync(Guid roomId)
    {
                if (_isFetchingMissingRoom)
        {
            Console.WriteLine($"[RoomsVM] ⏳ Already fetching room {roomId}, skipping duplicate call.");
            return;
        }

        _isFetchingMissingRoom = true;
        try
        {
            Console.WriteLine($"[RoomsVM] ⚠️ Room {roomId} missing! Waiting 300ms then reloading...");

                        await Task.Delay(300);

            await LoadAsync();
            Console.WriteLine($"[RoomsVM] ✅ Reload complete. Total rooms now: {Rooms.Count}");

                        if (_flags.ActiveRoomId != roomId)
            {
                _ = Task.Run(async () => { try { await _sound.PlayAsync(); } catch { } });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RoomsVM] ❌ Error in HandleMissingRoomAsync: {ex.Message}");
        }
        finally
        {
            _isFetchingMissingRoom = false;         }
    }
    public void UpdateRoomPreview(
     Guid roomId, Guid messageId, string? preview,
     Guid senderId, DateTime messageAt, MessageStatus status,
     bool skipUnread = false, bool skipNotify = false,
     string? senderName = null)      {
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);

        if (idx < 0)
        {
            Console.WriteLine($"[RoomsVM] Room {roomId} not found in UpdateRoomPreview.");
            _ = HandleMissingRoomAsync(roomId);
            return;
        }

        var r = list[idx];
        var truncated = preview?.Length > 60 ? preview[..60] + "…" : preview ?? "Message";

                if (senderId != Guid.Empty && !string.IsNullOrEmpty(senderName))
        {
            _knownMemberNames[senderId] = senderName;
        }

                var updatedMemberNames = new Dictionary<Guid, string>(r.MemberNames ?? new());
        if (senderId != Guid.Empty)
        {
            if (!string.IsNullOrEmpty(senderName))
                updatedMemberNames[senderId] = senderName;
            else if (_knownMemberNames.TryGetValue(senderId, out var cachedName))
                updatedMemberNames[senderId] = cachedName;
        }

        var updated = new RoomListItemModel
        {
            Id = r.Id,
            Name = r.Name,
            Type = r.Type,
            OtherUserId = r.OtherUserId,
            OtherDisplayName = r.OtherDisplayName,
            IsMuted = r.IsMuted,
            UnreadCount = skipUnread ? r.UnreadCount : r.UnreadCount + 1,
            LastMessageAt = messageAt,
            LastMessagePreview = truncated,
            LastMessageId = messageId,
            LastMessageSenderId = senderId,
            LastMessageStatus = status,
            MemberNames = updatedMemberNames          };

        list.RemoveAt(idx);
        list.Insert(0, updated);
        Rooms = list;
        ApplyFilter();

        bool isMuted = r.IsMuted || _flags.GetMuted(roomId);
        NotifyChanged();
    }
        public void CacheMemberName(Guid userId, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            _knownMemberNames[userId] = displayName;
    }
        public string? GetKnownMemberName(Guid userId)
    {
        if (_knownMemberNames.TryGetValue(userId, out var name))
            return name;
        return null;
    }

    private void OnMessageReceived(MessageModel msg)
    {
        Console.WriteLine($"[RoomsVM] 📨 MessageReceived: Room={msg.RoomId}, SenderId={msg.SenderId}");

        bool isSystemMessage = msg.SenderId == Guid.Empty;
        if (isSystemMessage)
        {
            HandleSystemMessage(msg);
            return;
        }

        bool isActiveRoom = _flags.ActiveRoomId == msg.RoomId;
        bool isMuted = _flags.GetMuted(msg.RoomId);
        bool isMyMessage = msg.SenderId == _cachedUserId;

        UpdateRoomPreview(
            msg.RoomId,
            msg.Id,
            msg.Content,
            msg.SenderId,
            msg.CreatedAt,
            MessageStatus.Sent,
            skipUnread: isActiveRoom  || isMyMessage,             skipNotify: true         );

               
    }
    private async void OnRoomUpdated(RoomUpdatedModel upd)
    {
        Console.WriteLine($"[RoomsVM] 🔴 RoomUpdated: Room={upd.RoomId}, MessageId={upd.MessageId}");
        Console.WriteLine($"[RoomsVM]    SenderId={upd.SenderId}, Preview='{upd.Preview}'");
        Console.WriteLine($"[RoomsVM]    IsSystem={upd.SenderId == Guid.Empty}");
        Console.WriteLine($"[CLEAR DEBUG] MessageId={upd.MessageId}, Preview='{upd.Preview}', SenderId={upd.SenderId}");
        Console.WriteLine($"[CLEAR DEBUG] IsEmpty={upd.MessageId == Guid.Empty}, IsPreviewEmpty={string.IsNullOrEmpty(upd.Preview)}");

                if (upd.IsClearEvent)
        {
            var clearList = Rooms.ToList();
            var clearIdx = clearList.FindIndex(r => r.Id == upd.RoomId);
            if (clearIdx >= 0)
            {
                var cr = clearList[clearIdx];
                clearList[clearIdx] = new RoomListItemModel
                {
                    Id = cr.Id,
                    Name = cr.Name,
                    Type = cr.Type,
                    OtherUserId = cr.OtherUserId,
                    OtherDisplayName = cr.OtherDisplayName,
                    IsMuted = cr.IsMuted,
                    UnreadCount = 0,
                    LastMessageAt = null,
                    LastMessagePreview = null,
                    LastMessageId = null,
                    LastMessageSenderId = null,
                    LastMessageStatus = null,
                    MemberNames = cr.MemberNames
                };

                                _flags.SetLastNonSystemPreview(upd.RoomId, null);
                _flags.SetLastReactionPreview(upd.RoomId, null);
                _flags.SetRoomCleared(upd.RoomId);
                _lastMessageStatusCache.Remove(upd.RoomId);

                Rooms = clearList;
                ApplyFilter();
                NotifyChanged();
            }
            return;
        }
        bool isDeletedMessage = upd.Preview == "🚫 This message was deleted";


        if (_flags.GetBlockedByMe(upd.SenderId) || _flags.GetBlockedMe(upd.SenderId))
        {
            Console.WriteLine($"[RoomsVM] 🚫 Blocked preview ignored for user: {upd.SenderId}");
            return;
        }

                if (!string.IsNullOrEmpty(upd.Preview) && upd.Preview.Contains("reacted"))
        {
            _flags.SetLastReactionPreview(upd.RoomId, upd.Preview);
        }

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == upd.RoomId);

        if (idx < 0)
        {
            Console.WriteLine($"[RoomsVM] Room {upd.RoomId} not found in OnRoomUpdated.");
            _ = HandleMissingRoomAsync(upd.RoomId);
            return;
        }

        var currentRoom = list[idx];

        bool isSystemMessage = upd.SenderId == Guid.Empty;
        bool iAmSender = upd.SenderId == _cachedUserId;

                bool updatedIsMuted = _flags.GetMuted(upd.RoomId) || upd.IsMuted;

        bool isActuallyNewMessage;

        if (isSystemMessage)
        {
            isActuallyNewMessage = !string.IsNullOrEmpty(upd.Preview) &&
                                   (currentRoom.LastMessagePreview != upd.Preview ||
                                    upd.CreatedAt > currentRoom.LastMessageAt);

            Console.WriteLine($"[RoomsVM] System message: isActuallyNewMessage={isActuallyNewMessage}");

            if (isActuallyNewMessage && !string.IsNullOrEmpty(upd.Preview))
            {
                Console.WriteLine($"[RoomsVM] ✅ System message is NEW: '{upd.Preview}'");
            }
        }
        else
        {
            isActuallyNewMessage = upd.MessageId != Guid.Empty &&
                                  (!currentRoom.LastMessageId.HasValue || upd.MessageId != currentRoom.LastMessageId.Value);

                        if (isActuallyNewMessage)
            {
                _flags.UnsetRoomCleared(upd.RoomId);
                Console.WriteLine($"[RoomsVM] Room {upd.RoomId} cleared flag removed due to new message");
            }
        }

        bool isActive = _flags.ActiveRoomId == upd.RoomId;
        var currentUnread = _flags.GetUnread(upd.RoomId);

        int nextUnread;
        if (isActive || iAmSender)
        {
            nextUnread = 0;
        }
        else
        {
            bool isReactionEvent = !string.IsNullOrEmpty(upd.Preview) && upd.Preview.Contains("reacted");
            bool isDeleteEvent = !string.IsNullOrEmpty(upd.Preview) && upd.Preview.Contains("deleted");

            if (isActuallyNewMessage && !isReactionEvent && !isDeleteEvent)
                nextUnread = currentUnread + 1;
            else if (!isActuallyNewMessage)
                nextUnread = upd.UnreadDelta < 0 ? 0 : currentUnread;
            else
                nextUnread = currentUnread; 

            nextUnread = Math.Max(0, nextUnread);
        }

        _flags.SetUnread(upd.RoomId, nextUnread);

        MessageStatus? lastMessageStatus = currentRoom.LastMessageStatus;
        string? finalPreview;
        Guid? messageId;
        Guid? senderId;
        DateTime? messageTime;

                if (!string.IsNullOrEmpty(upd.Preview))
        {
            finalPreview = upd.Preview;
        }
        else
        {
            finalPreview = currentRoom.LastMessagePreview;
        }

                if (isSystemMessage && string.IsNullOrEmpty(finalPreview))
        {
            var lastNonSystem = _flags.GetLastNonSystemPreview(upd.RoomId);
            if (!string.IsNullOrEmpty(lastNonSystem))
            {
                finalPreview = lastNonSystem;
                Console.WriteLine($"[RoomsVM] Using saved non-system preview for system message: '{finalPreview}'");
            }
            else
            {
                finalPreview = "System message";
            }
        }

                if (!isSystemMessage && !string.IsNullOrEmpty(finalPreview))
        {
            _flags.SetLastNonSystemPreview(upd.RoomId, finalPreview);
            Console.WriteLine($"[RoomsVM] Saved regular preview: '{finalPreview}'");
        }

                if (isSystemMessage)
        {
                        if (isActuallyNewMessage)
            {
                messageId = Guid.NewGuid();
                messageTime = upd.CreatedAt != DateTime.MinValue ? upd.CreatedAt : DateTime.UtcNow;
            }
            else
            {
                messageId = currentRoom.LastMessageId ?? Guid.NewGuid();
                messageTime = currentRoom.LastMessageAt;
            }
            senderId = Guid.Empty;
            lastMessageStatus = null;         }
        else
        {
            messageId = upd.MessageId != Guid.Empty ? upd.MessageId : currentRoom.LastMessageId;
            senderId = upd.SenderId != Guid.Empty ? upd.SenderId : currentRoom.LastMessageSenderId;
            messageTime = upd.CreatedAt != DateTime.MinValue ? upd.CreatedAt : currentRoom.LastMessageAt;

            if (isDeletedMessage)
            {
                lastMessageStatus = null;
                _lastMessageStatusCache.Remove(upd.RoomId);
            }
            else if (isActuallyNewMessage)
            {
                lastMessageStatus = iAmSender ? MessageStatus.Sent : (currentRoom.LastMessageStatus ?? MessageStatus.Sent);
                if (upd.MessageId != Guid.Empty)
                {
                    _lastMessageStatusCache[upd.RoomId] = (upd.MessageId, MessageStatus.Sent);
                }
            }
            else
            {
                lastMessageStatus = currentRoom.LastMessageStatus;
            }
        }

        var updatedMemberNames = new Dictionary<Guid, string>(currentRoom.MemberNames ?? new());

                if (upd.SenderId != Guid.Empty && !string.IsNullOrEmpty(upd.SenderName))
        {
            _knownMemberNames[upd.SenderId] = upd.SenderName;
            updatedMemberNames[upd.SenderId] = upd.SenderName;
        }
                else if (upd.SenderId != Guid.Empty && _knownMemberNames.TryGetValue(upd.SenderId, out var cached))
        {
            updatedMemberNames[upd.SenderId] = cached;
        }

        var updatedRoom = new RoomListItemModel
        {
            Id = currentRoom.Id,
            Name = currentRoom.Name,
            Type = currentRoom.Type,
            OtherUserId = currentRoom.OtherUserId,
            OtherDisplayName = currentRoom.OtherDisplayName,
            IsMuted = updatedIsMuted,
            UnreadCount = nextUnread,
            LastMessageAt = messageTime,
            LastMessagePreview = finalPreview,
            LastMessageId = messageId,
            LastMessageSenderId = senderId,
            MemberNames = updatedMemberNames,              LastMessageStatus = lastMessageStatus
        };
                if (isActuallyNewMessage)
        {
            Console.WriteLine($"[RoomsVM] ⬆️ Moving room to top due to new {(isSystemMessage ? "system" : "regular")} message");
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
    public void UpdateDeletedMessagePreview(Guid roomId, Guid messageId)
    {
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId && r.LastMessageId == messageId);
        if (idx < 0) return;

        _flags.SetLastNonSystemPreview(roomId, "🚫 This message was deleted");
        _flags.SetLastReactionPreview(roomId, null);

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
            LastMessagePreview = "🚫 This message was deleted",
            LastMessageId = room.LastMessageId,
            LastMessageSenderId = room.LastMessageSenderId,
            LastMessageStatus = null,
            MemberNames = room.MemberNames
        };

        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }
    public void RemoveRoomLocal(Guid roomId)
    {
        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == roomId);
        if (idx < 0) return;

        list.RemoveAt(idx);
        Rooms = list;
        ApplyFilter();
        NotifyChanged();
    }
    private void HandleSystemMessage(MessageModel msg)
    {
        Console.WriteLine($"[RoomsVM] 📨 Handling system message for room {msg.RoomId}: '{msg.Content}'");

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == msg.RoomId);

        if (idx < 0)
        {
            Console.WriteLine($"[RoomsVM] Room {msg.RoomId} not found for system message");
            return;
        }

        var room = list[idx];

                if (room.LastMessageSenderId != Guid.Empty && !string.IsNullOrEmpty(room.LastMessagePreview))
        {
            _flags.SetLastNonSystemPreview(msg.RoomId, room.LastMessagePreview);
            Console.WriteLine($"[RoomsVM] Saved regular preview: '{room.LastMessagePreview}'");
        }

                string systemPreview = FormatSystemPreview(msg.Content);
                bool isActive = _flags.ActiveRoomId == msg.RoomId;
        int newUnread = isActive ? 0 : room.UnreadCount + 1;

                var updatedRoom = new RoomListItemModel
        {
            Id = room.Id,
            Name = room.Name,
            Type = room.Type,
            OtherUserId = room.OtherUserId,
            OtherDisplayName = room.OtherDisplayName,
            IsMuted = room.IsMuted,
            UnreadCount = newUnread,
            LastMessageAt = msg.CreatedAt,
            LastMessagePreview = systemPreview,
            LastMessageId = Guid.NewGuid(),             LastMessageSenderId = Guid.Empty,
            MemberNames = room.MemberNames,
            LastMessageStatus = null
        };

                Console.WriteLine($"[RoomsVM] ⬆️ Moving room to top due to system message");
        list.RemoveAt(idx);
        list.Insert(0, updatedRoom);

        Rooms = list;
        _flags.SetUnread(msg.RoomId, newUnread);
        ApplyFilter();
        NotifyChanged();

        Console.WriteLine($"[RoomsVM] ✅ Room updated with system message: '{systemPreview}', unread={newUnread}");
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
            MemberNames = r.MemberNames,
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
            LastMessageStatus = r.LastMessageStatus,
             MemberNames = r.MemberNames
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
    
    private void OnTypingStarted(Guid roomId, Guid userId)
    {
        Console.WriteLine($"[RoomsVM] ✍️ TypingStarted for room {roomId}, user {userId}");

        lock (_typingStatus)
        {
            _typingStatus[roomId] = true;
        }

                UpdateRoomTypingStatus(roomId, true);

    }
   
    private void OnTypingStopped(Guid roomId, Guid userId)
    {
        Console.WriteLine($"[RoomsVM] ✋ TypingStopped for room {roomId}, user {userId}");

        lock (_typingStatus)
        {
            _typingStatus[roomId] = false;
        }

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
    }        public bool IsRoomTyping(Guid roomId)
    {
        lock (_typingStatus)
        {
            return _typingStatus.TryGetValue(roomId, out var isTyping) && isTyping;
        }
    }

}