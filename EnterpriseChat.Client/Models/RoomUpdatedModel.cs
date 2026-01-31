namespace EnterpriseChat.Client.Models;

public sealed class RoomUpdatedModel
{
    public Guid RoomId { get; set; }
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string Preview { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int UnreadDelta { get; set; }
}
