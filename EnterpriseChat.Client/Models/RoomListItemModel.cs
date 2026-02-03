namespace EnterpriseChat.Client.Models;

public sealed class RoomListItemModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public Guid? LastMessageSenderId { get; set; }
    public MessageStatus? LastMessageStatus { get; set; }

    public Guid? OtherUserId { get; init; }
    public string? OtherDisplayName { get; init; }

    // ✅ realtime fields (mutable)
    public int UnreadCount { get; set; }
    public bool IsMuted { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public Guid? LastMessageId { get; set; }
}
