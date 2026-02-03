using System.Linq;

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

    public List<MessageReceiptModel> Receipts { get; set; } = new();

    public int DeliveredCount => Receipts.Count(r => (int)r.Status >= (int)MessageStatus.Delivered);
    public int ReadCount => Receipts.Count(r => (int)r.Status == (int)MessageStatus.Read);
    public int TotalRecipients { get; set; } = 1;
}
