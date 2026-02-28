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
    public int ReadCount { get; set; }
    public int DeliveredCount { get; set; }
    public int TotalRecipients { get; set; }
    public ReplyInfoDto? ReplyInfo { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ✅ جديد: للرسائل النظامية
    public bool IsSystemMessage { get; set; } = false;
}