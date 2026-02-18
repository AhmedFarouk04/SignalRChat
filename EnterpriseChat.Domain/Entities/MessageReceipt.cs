using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.Domain.Entities;

public sealed class MessageReceipt
{
    public MessageId MessageId { get; private set; }
    public UserId UserId { get; private set; }

    // ✅ إضافة RoomId
    public RoomId RoomId { get; private set; }

    public MessageStatus Status { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private MessageReceipt() { }

    public MessageReceipt(
        MessageId messageId,
        RoomId roomId,  // ✅ إضافة parameter
        UserId userId)
    {
        MessageId = messageId;
        RoomId = roomId;  // ✅ تعيين القيمة
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