namespace EnterpriseChat.Client.Models;

public sealed class RoomUpdatedModel
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
    public bool IsSystemMessage { get; set; } = false;
    public string? SystemEventType { get; set; }   // "MemberAdded", "MemberRemoved", "UserJoined", "NameChanged", etc.
}
