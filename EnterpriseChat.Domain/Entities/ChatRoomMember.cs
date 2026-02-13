using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public sealed class ChatRoomMember
{
    public RoomId RoomId { get; private set; }
    public UserId UserId { get; private set; }

    // ✅ Shadow Property للـ EF Core (مش هتظهر في الـ Domain)
    // private Guid UserIdGuid { get; set; }

    public bool IsOwner { get; private set; }
    public bool IsAdmin { get; private set; }
    public DateTime JoinedAt { get; private set; }

    public MessageId? LastReadMessageId { get; private set; }
    public DateTime? LastReadAt { get; private set; }

    private ChatRoomMember() { }

    private ChatRoomMember(
        RoomId roomId,
        UserId userId,
        bool isOwner = false,
        bool isAdmin = false)
    {
        RoomId = roomId;
        UserId = userId;
        IsOwner = isOwner;
        IsAdmin = isAdmin;
        JoinedAt = DateTime.UtcNow;
    }

    public static ChatRoomMember Create(
        RoomId roomId,
        UserId userId,
        bool isOwner = false,
        bool isAdmin = false)
        => new(roomId, userId, isOwner, isAdmin);

    public void SetOwner(bool isOwner)
    {
        IsOwner = isOwner;
        if (isOwner) IsAdmin = true;
    }

    public void PromoteToAdmin() => IsAdmin = true;
    public void DemoteFromAdmin()
    {
        if (IsOwner) return;
        IsAdmin = false;
    }

    public void UpdateLastReadMessageId(MessageId messageId)
    {
        LastReadMessageId = messageId;
        LastReadAt = DateTime.UtcNow;
    }
}