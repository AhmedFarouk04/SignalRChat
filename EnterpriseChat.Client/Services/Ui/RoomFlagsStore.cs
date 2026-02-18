using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Http;

namespace EnterpriseChat.Client.Services.Ui;

public sealed class RoomFlagsStore
{
    private readonly Dictionary<Guid, bool> _mutedRooms = new();
    private readonly Dictionary<Guid, bool> _blockedByMe = new();
    private readonly Dictionary<Guid, bool> _blockedMe = new();
    private readonly Dictionary<Guid, int> _unreadByRoom = new();
    private readonly Dictionary<Guid, MessageStatus> _lastMessageStatus = new();

    private Guid? _activeRoomId;
    public Guid? ActiveRoomId => _activeRoomId;

    // ✅ Lock objects للتأكد من الـ thread safety
    private readonly object _blockedByMeLock = new();
    private readonly object _blockedMeLock = new();
    private readonly object _mutedLock = new();

    public event Action<Guid>? RoomMuteChanged;
    public event Action<Guid>? LastMessageStatusChanged;
    public event Action<Guid>? BlockedByMeChanged;
    public event Action<Guid>? BlockedMeChanged;
    public event Action<Guid?>? ActiveRoomChanged;
    public event Action<Guid>? RoomUnreadChanged;

    public bool GetMuted(Guid roomId)
    {
        lock (_mutedLock)
        {
            return _mutedRooms.TryGetValue(roomId, out var v) && v;
        }
    }

    public async Task LoadStateAsync(ModerationApi mod, ICurrentUser currentUser)
    {
        var userId = await currentUser.GetUserIdAsync();
        if (!userId.HasValue) return;

        // muted rooms
        var muted = await mod.GetMutedAsync();
        lock (_mutedLock)
        {
            foreach (var m in muted)
                _mutedRooms[m.RoomId] = true;
        }

        // ✅ blocked by me
        var blocked = await mod.GetBlockedAsync();
        lock (_blockedByMeLock)
        {
            _blockedByMe.Clear();
            foreach (var b in blocked)
                _blockedByMe[b.UserId] = true;
        }

        Console.WriteLine($"[Flags] Loaded {_mutedRooms.Count} muted rooms and {_blockedByMe.Count} blocked-by-me users from API");
    }

    public bool GetBlockedByMe(Guid userId)
    {
        lock (_blockedByMeLock)
        {
            return _blockedByMe.TryGetValue(userId, out var v) && v;
        }
    }

    public bool GetBlockedMe(Guid userId)
    {
        lock (_blockedMeLock)
        {
            return _blockedMe.TryGetValue(userId, out var v) && v;
        }
    }

    public bool GetAnyBlock(Guid userId)
    {
        return GetBlockedByMe(userId) || GetBlockedMe(userId);
    }

    public bool GetBlocked(Guid userId) => GetBlockedByMe(userId);

    public int GetUnread(Guid roomId)
        => _unreadByRoom.TryGetValue(roomId, out var v) ? v : 0;

    public MessageStatus? GetLastMessageStatus(Guid roomId)
    {
        return _lastMessageStatus.TryGetValue(roomId, out var status) ? status : null;
    }

    public void SetLastMessageStatus(Guid roomId, MessageStatus status)
    {
        _lastMessageStatus[roomId] = status;
        LastMessageStatusChanged?.Invoke(roomId);
    }

    public void SetMuted(Guid roomId, bool muted)
    {
        lock (_mutedLock)
        {
            _mutedRooms[roomId] = muted;
        }
        Console.WriteLine($"[Flags] SetMuted: room={roomId}, muted={muted}");
        RoomMuteChanged?.Invoke(roomId);
    }

    public void SetBlockedByMe(Guid userId, bool blocked)
    {
        bool changed;
        lock (_blockedByMeLock)
        {
            if (blocked)
            {
                changed = !_blockedByMe.ContainsKey(userId);
                _blockedByMe[userId] = true;
            }
            else
            {
                changed = _blockedByMe.Remove(userId);
            }
        }

        if (changed)
        {
            Console.WriteLine($"[Flags] SetBlockedByMe: user={userId}, blocked={blocked}");
            BlockedByMeChanged?.Invoke(userId);
        }
    }

    public void SetBlockedMe(Guid userId, bool blocked)
    {
        bool changed;
        lock (_blockedMeLock)
        {
            if (blocked)
            {
                changed = !_blockedMe.ContainsKey(userId);
                _blockedMe[userId] = true;
            }
            else
            {
                changed = _blockedMe.Remove(userId);
            }
        }

        if (changed)
        {
            Console.WriteLine($"[Flags] SetBlockedMe: user={userId}, blocked={blocked}");
            BlockedMeChanged?.Invoke(userId);
        }
    }

    public void SetBlocked(Guid userId, bool blocked) => SetBlockedByMe(userId, blocked);

    public void ClearAllBlocks()
    {
        lock (_blockedByMeLock)
        {
            _blockedByMe.Clear();
        }
        lock (_blockedMeLock)
        {
            _blockedMe.Clear();
        }
        Console.WriteLine("[Flags] All blocks cleared");
    }

    public void SetActiveRoom(Guid? roomId)
    {
        var oldRoom = _activeRoomId;
        _activeRoomId = roomId;
        Console.WriteLine($"[Flags] SetActiveRoom: {oldRoom} -> {roomId}");

        ActiveRoomChanged?.Invoke(roomId);

        if (roomId.HasValue)
        {
            Console.WriteLine($"[Flags] Active room set, clearing unread for {roomId.Value}");
            SetUnread(roomId.Value, 0);
        }
    }

    public void SetUnread(Guid roomId, int count)
    {
        if (count < 0) count = 0;

        if (_activeRoomId.HasValue && _activeRoomId.Value == roomId)
        {
            if (count > 0)
            {
                Console.WriteLine($"[Flags] Forcing unread=0 for active room {roomId}");
                count = 0;
            }
        }

        if (_unreadByRoom.TryGetValue(roomId, out var old) && old == count)
            return;

        _unreadByRoom[roomId] = count;
        Console.WriteLine($"[Flags] SetUnread: room={roomId}, count={count}, old={old}, active={_activeRoomId}");

        RoomUnreadChanged?.Invoke(roomId);
    }

    public void AddUnread(Guid roomId, int delta)
    {
        if (delta == 0) return;

        if (_activeRoomId.HasValue && _activeRoomId.Value == roomId)
        {
            Console.WriteLine($"[Flags] AddUnread ignored - active room {roomId}");
            SetUnread(roomId, 0);
            return;
        }

        var current = GetUnread(roomId);
        var next = current + delta;
        Console.WriteLine($"[Flags] AddUnread: room={roomId}, delta={delta}, {current} -> {next}");

        SetUnread(roomId, next);
    }

    public void SetBlockedUsers(IEnumerable<Guid> userIds)
    {
        var next = new HashSet<Guid>(userIds);
        List<Guid> added;
        List<Guid> removed;

        lock (_blockedByMeLock)
        {
            removed = _blockedByMe.Keys.Where(id => !next.Contains(id)).ToList();
            added = next.Where(id => !_blockedByMe.ContainsKey(id)).ToList();

            foreach (var id in removed)
                _blockedByMe.Remove(id);

            foreach (var id in added)
                _blockedByMe[id] = true;
        }

        foreach (var id in added)
            BlockedByMeChanged?.Invoke(id);

        foreach (var id in removed)
            BlockedByMeChanged?.Invoke(id);

        Console.WriteLine($"[Flags] SetBlockedUsers(by me): +{added.Count} -{removed.Count}");
    }

    // ✅ دالة جديدة للتحقق السريع مع الـ Logging
    public (bool blockedByMe, bool blockedMe) GetBlockStatus(Guid userId)
    {
        var byMe = GetBlockedByMe(userId);
        var me = GetBlockedMe(userId);

        if (byMe || me)
        {
            Console.WriteLine($"[Flags] Block status for {userId}: byMe={byMe}, me={me}");
        }

        return (byMe, me);
    }
}