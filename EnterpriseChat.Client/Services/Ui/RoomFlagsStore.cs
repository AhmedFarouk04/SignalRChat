using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Services.Http;

namespace EnterpriseChat.Client.Services.Ui;

public sealed class RoomFlagsStore
{
    private readonly Dictionary<Guid, bool> _mutedRooms = new();

    // ✅ فصل اتجاه البلوك:
    // أنا عامل بلوك لده
    private readonly Dictionary<Guid, bool> _blockedByMe = new();
    // ده عامل بلوك ليا
    private readonly Dictionary<Guid, bool> _blockedMe = new();

    private readonly Dictionary<Guid, int> _unreadByRoom = new();

    private Guid? _activeRoomId;
    public Guid? ActiveRoomId => _activeRoomId;

    public event Action<Guid>? RoomMuteChanged;

    // ✅ بدّل UserBlockChanged بحدثين أوضح
    public event Action<Guid>? BlockedByMeChanged;
    public event Action<Guid>? BlockedMeChanged;

    public event Action<Guid?>? ActiveRoomChanged;
    public event Action<Guid>? RoomUnreadChanged;

    public bool GetMuted(Guid roomId)
        => _mutedRooms.TryGetValue(roomId, out var v) && v;

    // ✅ تحميل الحالة من الـ API
    // ملاحظة: mod.GetBlockedAsync غالبًا بيرجع "اللي أنا عاملهم بلوك"
    public async Task LoadStateAsync(ModerationApi mod, ICurrentUser currentUser)
    {
        var userId = await currentUser.GetUserIdAsync();
        if (!userId.HasValue) return;

        // muted rooms
        var muted = await mod.GetMutedAsync();
        foreach (var m in muted)
            _mutedRooms[m.RoomId] = true;

        // ✅ blocked by me (الناس اللي أنا عاملهم block)
        var blocked = await mod.GetBlockedAsync();
        _blockedByMe.Clear();
        foreach (var b in blocked)
            _blockedByMe[b.UserId] = true;

        Console.WriteLine($"[Flags] Loaded {_mutedRooms.Count} muted rooms and {_blockedByMe.Count} blocked-by-me users from API");
    }

    // ✅ APIs جديدة
    public bool GetBlockedByMe(Guid userId)
        => _blockedByMe.TryGetValue(userId, out var v) && v;

    public bool GetBlockedMe(Guid userId)
        => _blockedMe.TryGetValue(userId, out var v) && v;

    // ✅ لو عايز “اقفل الإرسال” في أي اتجاه
    public bool GetAnyBlock(Guid userId)
        => GetBlockedByMe(userId) || GetBlockedMe(userId);

    // ✅ Backward compatibility (لو في كود قديم بيستدعي GetBlocked)
    // خليها "أنا عامل بلوك"
    public bool GetBlocked(Guid userId) => GetBlockedByMe(userId);

    public int GetUnread(Guid roomId)
        => _unreadByRoom.TryGetValue(roomId, out var v) ? v : 0;

    public void SetMuted(Guid roomId, bool muted)
    {
        _mutedRooms[roomId] = muted;
        Console.WriteLine($"[Flags] SetMuted: room={roomId}, muted={muted}");
        RoomMuteChanged?.Invoke(roomId);
    }

    // ✅ أنا اللي بعمل بلوك
    public void SetBlockedByMe(Guid userId, bool blocked)
    {
        if (blocked) _blockedByMe[userId] = true;
        else _blockedByMe.Remove(userId);

        Console.WriteLine($"[Flags] SetBlockedByMe: user={userId}, blocked={blocked}");
        BlockedByMeChanged?.Invoke(userId);
    }

    // ✅ الطرف التاني عامل بلوك ليا
    public void SetBlockedMe(Guid userId, bool blocked)
    {
        if (blocked) _blockedMe[userId] = true;
        else _blockedMe.Remove(userId);

        Console.WriteLine($"[Flags] SetBlockedMe: user={userId}, blocked={blocked}");
        BlockedMeChanged?.Invoke(userId);
    }

    // ✅ Backward compatibility (لو في كود قديم بيستدعي SetBlocked)
    // اعتبره SetBlockedByMe
    public void SetBlocked(Guid userId, bool blocked) => SetBlockedByMe(userId, blocked);

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

    // ✅ تحميل bulk "blocked by me"
    public void SetBlockedUsers(IEnumerable<Guid> userIds)
    {
        var next = new HashSet<Guid>(userIds);

        var removed = _blockedByMe.Keys.Where(id => !next.Contains(id)).ToList();
        var added = next.Where(id => !_blockedByMe.ContainsKey(id)).ToList();

        foreach (var id in removed)
            _blockedByMe.Remove(id);

        foreach (var id in added)
            _blockedByMe[id] = true;

        foreach (var id in added)
            BlockedByMeChanged?.Invoke(id);

        foreach (var id in removed)
            BlockedByMeChanged?.Invoke(id);

        Console.WriteLine($"[Flags] SetBlockedUsers(by me): +{added.Count} -{removed.Count}");
    }
}
