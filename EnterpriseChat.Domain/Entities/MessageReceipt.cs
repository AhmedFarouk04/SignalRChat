using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public sealed class MessageReceipt
{
    public MessageId MessageId { get; private set; }
    public UserId UserId { get; private set; }

    public MessageStatus Status { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private MessageReceipt() { }

    public MessageReceipt(
        MessageId messageId,
        UserId userId)
    {
        MessageId = messageId;
        UserId = userId;
        Status = MessageStatus.Sent;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        if (Status >= MessageStatus.Delivered)
            return;

        Status = MessageStatus.Delivered;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRead()
    {
        if (Status >= MessageStatus.Read)
            return;

        Status = MessageStatus.Read;
        UpdatedAt = DateTime.UtcNow;
    }
}
