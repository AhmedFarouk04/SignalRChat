using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public sealed class ChatRoomMember
{
    public RoomId RoomId { get; private set; }
    public UserId UserId { get; private set; }

    public bool IsOwner { get; private set; }
    public bool IsAdmin { get; private set; }   

    public DateTime JoinedAt { get; private set; }

    private ChatRoomMember() { }

    private ChatRoomMember(
        RoomId roomId,
        UserId userId,
        bool isOwner,
        bool isAdmin)
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

    public void PromoteToAdmin()
    {
        IsAdmin = true;
    }

    public void DemoteFromAdmin()
    {
        if (IsOwner) return; 
        IsAdmin = false;
    }

    public void SetOwner(bool isOwner)
    {
        IsOwner = isOwner;
    }
}

