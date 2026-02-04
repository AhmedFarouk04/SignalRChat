using EnterpriseChat.Domain.Enums;
namespace EnterpriseChat.Application.DTOs;

public sealed class RoomListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;

    public Guid? OtherUserId { get; init; }
    public string? OtherDisplayName { get; init; }

    public int UnreadCount { get; init; }
    public bool IsMuted { get; init; }

    public DateTime? LastMessageAt { get; init; }
    public string? LastMessagePreview { get; init; }
    public Guid? LastMessageId { get; init; }

    // ✅ NEW
    public Guid? LastMessageSenderId { get; init; }
    public MessageStatus? LastMessageStatus { get; init; } // only meaningful when last msg is mine
}
