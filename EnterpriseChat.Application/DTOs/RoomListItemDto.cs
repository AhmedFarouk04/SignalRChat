using EnterpriseChat.Domain.Enums;
namespace EnterpriseChat.Application.DTOs;

public sealed class RoomListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;

    public Guid? OtherUserId { get; init; }
    public string? OtherDisplayName { get; init; }
    public string? LastReactionPreview { get; set; }
    public int UnreadCount { get; init; }
    public bool IsMuted { get; init; }

    public DateTime? LastMessageAt { get; init; }
    public string? LastMessagePreview { get; init; }
    public Guid? LastMessageId { get; init; }

    // ✅ NEW
    public Guid? LastReadMessageId { get; set; }
    public DateTime? LastReadAt { get; set; }
    public Guid? LastMessageSenderId { get; init; }
    public MessageStatus? LastMessageStatus { get; init; }
    public int? LastMessageTotalRecipients { get; init; }
    public int? LastMessageDeliveredCount { get; init; }
    public int? LastMessageReadCount { get; init; }
    public DateTime? LastSeenAt { get; set; } // ➕ أضف هذا

    public Dictionary<Guid, string> MemberNames { get; init; } = new();
}
