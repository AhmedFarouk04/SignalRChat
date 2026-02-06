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
    public MessageId? ReplyToMessageId { get; private set; }
    public Message? ReplyToMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    private readonly List<Reaction> _reactions = new();
    public IReadOnlyCollection<Reaction> Reactions => _reactions.AsReadOnly();
    private Message() { }

    public Message(
        RoomId roomId,
        UserId senderId,
        string content,
        IEnumerable<UserId> recipients,
         MessageId? replyToMessageId = null)
    {
        Id = MessageId.New();
        RoomId = roomId;
        SenderId = senderId;
        Content = content;
        CreatedAt = DateTime.UtcNow;
        ReplyToMessageId = replyToMessageId;


        foreach (var userId in recipients)
        {
            _receipts.Add(new MessageReceipt(Id, userId));
        }

        AddDomainEvent(new MessageSentEvent(
            Id,
            RoomId,
            SenderId,
            Content,
            CreatedAt,
            replyToMessageId
        ));
    }

    public void MarkDelivered(UserId userId)
    {
        var receipt = _receipts.FirstOrDefault(r => r.UserId == userId);

        if (receipt == null)
            return;

        if (receipt.Status >= MessageStatus.Delivered)
            return;

        receipt.MarkDelivered();

        AddDomainEvent(new MessageDeliveredEvent(Id, userId));
    }


    public void MarkRead(UserId userId)
    {
        var receipt = _receipts.FirstOrDefault(r => r.UserId == userId);

        if (receipt == null)
            return;

        if (receipt.Status == MessageStatus.Read)
            return;

        receipt.MarkRead();

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

    public void AddOrUpdateReaction(UserId userId, ReactionType reactionType)
    {
        var existing = _reactions.FirstOrDefault(r => r.UserId == userId);

        if (existing != null)
        {
            if (existing.Type == reactionType)
            {
                // نفس الـ reaction → احذفه (toggle)
                _reactions.Remove(existing);
            }
            else
            {
                // reaction مختلف → عدله
                existing.UpdateType(reactionType);
            }
        }
        else
        {
            // reaction جديد
            _reactions.Add(new Reaction(Id, userId, reactionType));
        }
    }
}
