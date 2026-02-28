using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Enums;

public sealed class MessageReadDto
{
    public Guid Id { get; init; }
    public Guid RoomId { get; init; }
    public Guid SenderId { get; init; }
    public string Content { get; init; } = string.Empty;

    // القديم (يمكن نحتفظ به للـ group info لو عايزين)
    public MessageStatus GlobalStatus { get; init; }  // اختياري – لو عايز تحتفظ بالحساب القديم

    // الجديد – ده اللي هنستخدمه في الـ UI
    public MessageStatus PersonalStatus { get; init; }

    public DateTime CreatedAt { get; init; }
    public List<MessageReceiptDto> Receipts { get; set; } = new();

    // اختياري: لو عايز تعرض عدد اللي وصلوا
    public int DeliveredCount { get; init; }
    public int ReadCount { get; init; }
    public int TotalRecipients { get; init; }
    public MessageReactionsDto? Reactions { get; init; }

    public bool IsEdited { get; init; }
    public bool IsDeleted { get; init; }
    public Guid? ReplyToMessageId { get; init; }
    public ReplyInfoDto? ReplyInfo { get; init; }
    public bool IsSystem { get; init; }
    public string? Type { get; init; }




}