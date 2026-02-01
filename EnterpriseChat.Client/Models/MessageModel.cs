using EnterpriseChat.Application.DTOs; // جديد عشان MessageReceiptDto
using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.Client.Models;

public class MessageModel
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public string? Error { get; set; }

    // ✅ جديد: Receipts per user (من DTO)
    public List<MessageReceiptDto> Receipts { get; set; } = new();

    // ✅ جديد: Counts (مع cast لـ int عشان fix CS0019)
    public int DeliveredCount => Receipts.Count(r => (int)r.Status >= (int)MessageStatus.Delivered);
    public int ReadCount => Receipts.Count(r => (int)r.Status == (int)MessageStatus.Read);
    public int TotalRecipients { get; set; } = 1; // default private, هيحسب في VM لgroups
}