using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public sealed class MutedRoom
{
    public RoomId RoomId { get; private set; }
    public UserId UserId { get; private set; }
    public DateTime MutedAt { get; private set; }

    private MutedRoom() { }

    private MutedRoom(RoomId roomId, UserId userId)
    {
        RoomId = roomId;
        UserId = userId;
        MutedAt = DateTime.UtcNow;
    }

    public static MutedRoom Create(RoomId roomId, UserId userId)
        => new(roomId, userId);
}
