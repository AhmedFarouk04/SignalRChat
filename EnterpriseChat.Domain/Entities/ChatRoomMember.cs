using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public sealed class ChatRoomMember
{
    public RoomId RoomId { get; private set; }
    public UserId UserId { get; private set; }
    public bool IsOwner { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private ChatRoomMember() { }

    private ChatRoomMember(
        RoomId roomId,
        UserId userId,
        bool isOwner)
    {
        RoomId = roomId;
        UserId = userId;
        IsOwner = isOwner;
        JoinedAt = DateTime.UtcNow;
    }

    public static ChatRoomMember Create(
        RoomId roomId,
        UserId userId,
        bool isOwner = false)
        => new(roomId, userId, isOwner);
}
