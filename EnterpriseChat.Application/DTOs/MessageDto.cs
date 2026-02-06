// EnterpriseChat.Application/DTOs/MessageDto.cs
using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.Application.DTOs;

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public MessageStatus Status { get; set; }
    public MessageReactionsDto? Reactions { get; set; }

    // ✅ أضف هذه الخصائص للـ Delivery Tracking:
    public int ReadCount { get; set; }
    public int DeliveredCount { get; set; }
    public int TotalRecipients { get; set; }

    // ✅ أضف خصائص الردود:
    public ReplyInfoDto? ReplyInfo { get; set; }
    public Guid? ReplyToMessageId { get; set; }

    // ✅ أضف خصائص Edit/Delete:
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? UpdatedAt { get; set; }
}