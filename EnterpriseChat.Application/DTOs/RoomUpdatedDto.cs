namespace EnterpriseChat.Application.DTOs;

public sealed class RoomUpdatedDto
{
    public Guid RoomId { get; init; }
    public Guid MessageId { get; init; }
    public Guid SenderId { get; init; }
    public string Preview { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int UnreadDelta { get; init; } // recipients +1, sender 0
}
