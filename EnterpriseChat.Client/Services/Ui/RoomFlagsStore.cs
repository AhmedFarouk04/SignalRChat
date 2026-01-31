namespace EnterpriseChat.Client.Services.Ui;

public sealed class RoomFlagsStore
{
    private readonly Dictionary<Guid, bool> _mutedRooms = new();
    private readonly Dictionary<Guid, bool> _blockedUsers = new();

    // ✅ جديد: unread per room (client truth)
    private readonly Dictionary<Guid, int> _unreadByRoom = new();

    private Guid? _activeRoomId;
    public Guid? ActiveRoomId => _activeRoomId;

    public event Action<Guid>? RoomMuteChanged;     // roomId
    public event Action<Guid>? UserBlockChanged;    // userId
    public event Action<Guid?>? ActiveRoomChanged;  // active room id

    // ✅ جديد: unread changed
    public event Action<Guid>? RoomUnreadChanged;   // roomId

    public bool GetMuted(Guid roomId)
        => _mutedRooms.TryGetValue(roomId, out var v) && v;

    public bool GetBlocked(Guid userId)
        => _blockedUsers.TryGetValue(userId, out var v) && v;

    // ✅ جديد: read current unread
    public int GetUnread(Guid roomId)
        => _unreadByRoom.TryGetValue(roomId, out var v) ? v : 0;

    public void SetMuted(Guid roomId, bool muted)
    {
        _mutedRooms[roomId] = muted;
        RoomMuteChanged?.Invoke(roomId);
    }

    public void SetBlocked(Guid userId, bool blocked)
    {
        if (blocked) _blockedUsers[userId] = true;
        else _blockedUsers.Remove(userId);

        UserBlockChanged?.Invoke(userId);
    }


    public void SetActiveRoom(Guid? roomId)
    {
        _activeRoomId = roomId;
        ActiveRoomChanged?.Invoke(roomId);

        // ✅ لو فتحت روم: صفّر unread فورًا (بدون refresh)
        if (roomId.HasValue)
            SetUnread(roomId.Value, 0);
    }

    // ✅ جديد: set absolute unread
    public void SetUnread(Guid roomId, int count)
    {
        if (count < 0) count = 0;

        // لو مفيش تغيير ما تبعتش event
        if (_unreadByRoom.TryGetValue(roomId, out var old) && old == count)
            return;

        _unreadByRoom[roomId] = count;
        RoomUnreadChanged?.Invoke(roomId);
    }

    // ✅ جديد: apply delta (مع حماية لو active)
    public void AddUnread(Guid roomId, int delta)
    {
        if (delta == 0) return;

        // لو انا جوه نفس الروم: unread لازم يفضل 0
        if (_activeRoomId.HasValue && _activeRoomId.Value == roomId)
        {
            SetUnread(roomId, 0);
            return;
        }

        var next = GetUnread(roomId) + delta;
        SetUnread(roomId, next);
    }

    public void SetBlockedUsers(IEnumerable<Guid> userIds)
    {
        _blockedUsers.Clear();
        foreach (var id in userIds)
            _blockedUsers[id] = true;

        // اختياري: نطلق event لكل واحد لو عايز UI يحدث فورًا
        foreach (var id in userIds)
            UserBlockChanged?.Invoke(id);
    }

}
