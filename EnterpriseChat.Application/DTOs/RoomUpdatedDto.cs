namespace EnterpriseChat.Application.DTOs;

public sealed class RoomUpdatedDto
{
    public Guid RoomId { get; set; }
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string Preview { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int UnreadDelta { get; set; }

        public string? RoomName { get; set; }
    public string? RoomType { get; set; }
    public string? SenderName { get; init; } 
    public bool IsReply { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public bool IsClearEvent { get; set; }     public bool IsMuted { get; set; }

    public bool IsSystemMessage { get; set; } 
}
