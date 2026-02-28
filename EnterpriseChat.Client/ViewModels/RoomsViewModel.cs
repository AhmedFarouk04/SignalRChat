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
    }
    private void OnMessageReactionUpdated(Guid messageId, Guid reactorId, int reactionTypeInt, bool isNewReaction)
    {
        // مش مهم في الـ RoomsVM نعمل حاجة هنا
        // التحديث هييجي عن طريق RoomUpdated من السيرفر
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

                var list = Rooms.ToList();
                var idx = list.FindIndex(x => x.Id == r.Id);
                if (idx < 0) continue;

                var room = list[idx];
                bool changed = false;

                // ✅ استرجاع الحالة المحفوظة
                var savedStatus = _flags.GetLastMessageStatus(r.Id);
                if (savedStatus.HasValue && room.LastMessageStatus != savedStatus.Value)
                {
                    room.LastMessageStatus = savedStatus.Value;
                    changed = true;
                }

                // ✅ استرجاع Reaction Preview
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

            // ✅ Reaction Preview له أولوية قصوى
            var savedReactionPreview = _flags.GetLastReactionPreview(fresh.Id);
            if (!string.IsNullOrEmpty(savedReactionPreview))
            {
                previewToUse = savedReactionPreview;
            }
            else if (fresh.LastMessageSenderId == Guid.Empty)
            {
                // رسالة نظام => استخدم آخر Preview غير نظامي
                var savedPreview = _flags.GetLastNonSystemPreview(fresh.Id);
                previewToUse = savedPreview ?? fresh.LastMessagePreview;
            }
            else
            {
                // رسالة عادية => خزنها
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
                // ✅ استخدم الـ Preview المحفوظ في الـ Store
                string? previewToUse = fresh.LastMessagePreview;

                // لو الرسالة الحالية من النظام، استخدم آخر Preview غير نظامي
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
                    // رسالة عادية => خزنها للمستقبل
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
                    LastMessagePreview = previewToUse,  // ✅ استخدم القيمة المعدلة
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
        Console.WriteLine($"[RoomsVM] 🔵 RoomUpserted: Room={dto.Id}, Name={dto.Name}");
        Console.WriteLine($"[RoomsVM]    LastMessageId={dto.LastMessageId}, SenderId={dto.LastMessageSenderId}");
        Console.WriteLine($"[RoomsVM]    Preview='{dto.LastMessagePreview}'");
        Console.WriteLine($"[RoomsVM]    IsSystem={dto.LastMessageSenderId == Guid.Empty}");

        var list = Rooms.ToList();
        var idx = list.FindIndex(r => r.Id == dto.Id);

        // ✅ تحقق إذا كانت آخر رسالة هي رسالة نظام
        bool isSystemMessage = dto.LastMessageSenderId == Guid.Empty;

        // ✅ جلب آخر Preview غير نظامي من الذاكرة أو من الـ store
        string? lastNonSystemPreview = _flags.GetLastNonSystemPreview(dto.Id);

        // لو لسة مخزنش حاجة، خدها من الغرفة الحالية
        if (lastNonSystemPreview == null && idx >= 0)
        {
            var existingRoom = list[idx];
            if (existingRoom.LastMessageSenderId != Guid.Empty)
            {
                lastNonSystemPreview = existingRoom.LastMessagePreview;
                _flags.SetLastNonSystemPreview(dto.Id, lastNonSystemPreview);
            }
        }

        // ✅ القرار الجديد:
        string? finalPreview;

        if (isSystemMessage)
        {
            // رسالة نظام => استخدم آخر Preview غير نظامي
            finalPreview = lastNonSystemPreview;
            Console.WriteLine($"[RoomsVM] System message upsert: keeping last non-system preview = '{finalPreview}'");
        }
        else
        {
            // رسالة عادية => استخدم Preview اللي جاي وخزنه
            finalPreview = dto.LastMessagePreview;
            _flags.SetLastNonSystemPreview(dto.Id, finalPreview);
            Console.WriteLine($"[RoomsVM] Regular message: saving preview = '{finalPreview}'");
        }

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
            LastMessagePreview = finalPreview,
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
    // في RoomsViewModel.cs

    private void OnMessageReceived(MessageModel msg)
    {
        Console.WriteLine($"[RoomsVM] 📨 MessageReceived: Room={msg.RoomId}, MsgId={msg.Id}, Content='{msg.Content}', SenderId={msg.SenderId}");

        bool isSystemMessage = msg.SenderId == Guid.Empty;

        // ✅ تعديل 1: معالجة رسائل النظام بشكل منفصل
        if (isSystemMessage)
        {
            Console.WriteLine($"[RoomsVM] System message received, updating room preview");
            HandleSystemMessage(msg);
            return;
        }

        // باقي الكود للرسائل العادية (الصوت والإشعارات)
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

    // ✅ دالة جديدة لمعالجة رسائل النظام

    // ✅ دالة مساعدة لتنسيق رسائل النظام
    private string FormatSystemMessagePreview(MessageModel msg)
    {
        // هنا يمكنك تنسيق رسالة النظام بناءً على محتواها
        // مثال: "تمت إضافة أحمد إلى المجموعة"

        if (string.IsNullOrEmpty(msg.Content))
            return "System message";

        // يمكنك إضافة منطق إضافي لتنسيق الرسالة
        return msg.Content.Length > 50 ? msg.Content.Substring(0, 47) + "..." : msg.Content;
    }

    // ✅ تعديل OnRoomUpdated لمعالجة رسائل النظام بشكل أفضل
    // في RoomsViewModel.cs - تعديل OnRoomUpdated

    // في RoomsViewModel.cs - تعديل OnRoomUpdated

    private async void OnRoomUpdated(RoomUpdatedModel upd)
    {
        Console.WriteLine($"[RoomsVM] 🔴 RoomUpdated: Room={upd.RoomId}, MessageId={upd.MessageId}");
        Console.WriteLine($"[RoomsVM]    SenderId={upd.SenderId}, Preview='{upd.Preview}'");
        Console.WriteLine($"[RoomsVM]    IsSystem={upd.SenderId == Guid.Empty}");

        // ✅ الحظر
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
        if (idx < 0) return;

        var r = list[idx];

        bool isSystemMessage = upd.SenderId == Guid.Empty;

        // ✅ تعديل مهم جداً: رسائل النظام تعتبر جديدة دائماً إذا كان فيها محتوى
        bool isActuallyNewMessage;

        if (isSystemMessage)
        {
            // رسالة نظام: نعتبرها جديدة إذا:
            // 1. المحتوى مختلف عن آخر رسالة نظام، أو
            // 2. الوقت أحدث من آخر تحديث
            isActuallyNewMessage = !string.IsNullOrEmpty(upd.Preview) &&
                                   (r.LastMessagePreview != upd.Preview ||
                                    upd.CreatedAt > r.LastMessageAt);

            Console.WriteLine($"[RoomsVM] System message: isActuallyNewMessage={isActuallyNewMessage}");

            // ✅ Force new message إذا كان المحتوى مختلف
            if (isActuallyNewMessage && !string.IsNullOrEmpty(upd.Preview))
            {
                Console.WriteLine($"[RoomsVM] ✅ System message is NEW: '{upd.Preview}'");
            }
        }
        else
        {
            // رسالة عادية: نستخدم المعيار العادي
            isActuallyNewMessage = upd.MessageId != Guid.Empty &&
                                  (!r.LastMessageId.HasValue || upd.MessageId != r.LastMessageId.Value);
        }

        var isActive = _flags.ActiveRoomId == upd.RoomId;
        var currentUnread = _flags.GetUnread(upd.RoomId);

        // ✅ تعديل: رسائل النظام تزيد unread count
        int nextUnread;
        if (isActive)
        {
            nextUnread = 0;
        }
        else
        {
            if (isSystemMessage && isActuallyNewMessage)
            {
                // رسالة نظام جديدة تزيد unread count
                nextUnread = currentUnread + 1;
                Console.WriteLine($"[RoomsVM] System message: increasing unread from {currentUnread} to {nextUnread}");
            }
            else
            {
                // استخدم الـ delta العادي
                nextUnread = upd.UnreadDelta < 0 ? 0 : currentUnread + upd.UnreadDelta;
            }
            nextUnread = Math.Max(0, nextUnread);
        }

        _flags.SetUnread(upd.RoomId, nextUnread);

        MessageStatus? lastMessageStatus = r.LastMessageStatus;
        string? finalPreview;
        Guid? messageId;
        Guid? senderId;
        DateTime? messageTime;

        if (isSystemMessage)
        {
            // ✅ رسالة نظام: احفظ Preview الرسالة العادية السابقة
            if (r.LastMessageSenderId != Guid.Empty && !string.IsNullOrEmpty(r.LastMessagePreview))
            {
                _flags.SetLastNonSystemPreview(upd.RoomId, r.LastMessagePreview);
                Console.WriteLine($"[RoomsVM] Saved last non-system preview: '{r.LastMessagePreview}'");
            }

            // ✅ استخدم Preview رسالة النظام - مهم جداً
            if (isActuallyNewMessage && !string.IsNullOrEmpty(upd.Preview))
            {
                finalPreview = upd.Preview;
                Console.WriteLine($"[RoomsVM] Using new system preview: '{finalPreview}'");
            }
            else if (!string.IsNullOrEmpty(upd.Preview))
            {
                finalPreview = upd.Preview;
                Console.WriteLine($"[RoomsVM] Using existing system preview: '{finalPreview}'");
            }
            else
            {
                finalPreview = r.LastMessagePreview ?? "System message";
                Console.WriteLine($"[RoomsVM] Using fallback preview: '{finalPreview}'");
            }

            // ✅ مهم جداً: استخدم MessageId جديد لرسالة النظام
            messageId = isActuallyNewMessage ? Guid.NewGuid() : (r.LastMessageId ?? Guid.NewGuid());
            senderId = Guid.Empty;
            messageTime = isActuallyNewMessage ?
                (upd.CreatedAt != DateTime.MinValue ? upd.CreatedAt : DateTime.UtcNow) :
                r.LastMessageAt;

            Console.WriteLine($"[RoomsVM] System message: final preview='{finalPreview}', new messageId={messageId}");
            lastMessageStatus = null; // رسائل النظام ليس لها حالة
        }
        else if (isActuallyNewMessage)
        {
            // رسالة عادية جديدة
            finalPreview = !string.IsNullOrEmpty(upd.Preview) ? upd.Preview : r.LastMessagePreview;
            lastMessageStatus = MessageStatus.Sent;
            messageId = upd.MessageId;
            senderId = upd.SenderId;
            messageTime = upd.CreatedAt != DateTime.MinValue ? upd.CreatedAt : r.LastMessageAt;

            _lastMessageStatusCache[upd.RoomId] = (upd.MessageId, MessageStatus.Sent);

            // خزن الـ Preview للرسائل العادية
            if (!string.IsNullOrEmpty(finalPreview))
            {
                _flags.SetLastNonSystemPreview(upd.RoomId, finalPreview);
            }
            Console.WriteLine($"[RoomsVM] Regular message: preview = '{finalPreview}'");
        }
        else
        {
            // مش رسالة جديدة
            finalPreview = r.LastMessagePreview;
            messageId = r.LastMessageId;
            senderId = r.LastMessageSenderId;
            messageTime = r.LastMessageAt;
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
            LastMessageAt = messageTime,
            LastMessagePreview = finalPreview,
            LastMessageId = messageId,
            LastMessageSenderId = senderId,
            MemberNames = r.MemberNames,
            LastMessageStatus = lastMessageStatus
        };

        // ✅ دائمًا حرك الغرفة للأعلى عند استلام رسالة جديدة (حتى لو نظامية)
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

    // ✅ تحسين HandleSystemMessage في OnMessageReceived
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

        // ✅ حفظ الـ Preview الحالي للرسائل العادية
        if (room.LastMessageSenderId != Guid.Empty && !string.IsNullOrEmpty(room.LastMessagePreview))
        {
            _flags.SetLastNonSystemPreview(msg.RoomId, room.LastMessagePreview);
            Console.WriteLine($"[RoomsVM] Saved regular preview: '{room.LastMessagePreview}'");
        }

        // ✅ إنشاء Preview مناسب لرسالة النظام
        string systemPreview = !string.IsNullOrEmpty(msg.Content) ? msg.Content : "System message";

        // ✅ حساب unread count
        bool isActive = _flags.ActiveRoomId == msg.RoomId;
        int newUnread = isActive ? 0 : room.UnreadCount + 1;

        // ✅ تحديث الغرفة مع رسالة النظام
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
            LastMessageId = Guid.NewGuid(), // ✅ استخدام GUID جديد
            LastMessageSenderId = Guid.Empty,
            MemberNames = room.MemberNames,
            LastMessageStatus = null
        };

        // ✅ نقل الغرفة لأعلى القائمة
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