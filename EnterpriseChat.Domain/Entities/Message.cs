using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Events;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public class Message
{
    private readonly List<DomainEvent> _domainEvents = new();
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private readonly List<MessageReceipt> _receipts = new();
    public IReadOnlyCollection<MessageReceipt> Receipts => _receipts.AsReadOnly();

    public MessageId Id { get; private set; }
    public RoomId RoomId { get; private set; }
    public UserId SenderId { get; private set; }
    public string Content { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Message() { }

    public Message(
        RoomId roomId,
        UserId senderId,
        string content,
        IEnumerable<UserId> recipients)
    {
        Id = MessageId.New();
        RoomId = roomId;
        SenderId = senderId;
        Content = content;
        CreatedAt = DateTime.UtcNow;

        foreach (var userId in recipients)
        {
            _receipts.Add(new MessageReceipt(Id, userId));
        }

        AddDomainEvent(new MessageSentEvent(
            Id,
            RoomId,
            SenderId,
            Content,
            CreatedAt
        ));
    }

    public void MarkDelivered(UserId userId)
    {
        var receipt = _receipts.FirstOrDefault(r => r.UserId == userId);
        receipt?.MarkDelivered();

        AddDomainEvent(new MessageDeliveredEvent(Id, userId));
    }

    public void MarkRead(UserId userId)
    {
        var receipt = _receipts.FirstOrDefault(r => r.UserId == userId);
        receipt?.MarkRead();

        AddDomainEvent(new MessageReadEvent(Id, userId));
    }

    private void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
