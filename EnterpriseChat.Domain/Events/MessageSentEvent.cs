using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Events;

public sealed class MessageSentEvent : DomainEvent
{
    public MessageId MessageId { get; }
    public RoomId RoomId { get; }
    public UserId SenderId { get; }
    public string Content { get; }
    public DateTime CreatedAt { get; }

    public MessageSentEvent(
        MessageId messageId,
        RoomId roomId,
        UserId senderId,
        string content,
        DateTime createdAt)
    {
        MessageId = messageId;
        RoomId = roomId;
        SenderId = senderId;
        Content = content;
        CreatedAt = createdAt;
    }
}
