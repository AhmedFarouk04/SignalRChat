namespace EnterpriseChat.Application.DTOs;

public sealed class RoomUpdatedDto
{
    public Guid RoomId { get; set; }
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string Preview { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int UnreadDelta { get; set; }

    // ✅ add these
    public string? RoomName { get; set; }
    public string? RoomType { get; set; }

    public bool IsReply { get; set; }
    public Guid? ReplyToMessageId { get; set; }
}
