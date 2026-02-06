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

    // ✅ غير read-only
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int TotalRecipients { get; set; } = 1;
    public bool HasReplies { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public MessageReactionsModel? Reactions { get; set; }

    public Guid? ReplyToMessageId { get; set; }
    public ReplyInfoModel? ReplyInfo { get; set; }




    

}