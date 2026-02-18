using EnterpriseChat.Domain.Common;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Events;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Domain.Enums;

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
    public bool IsEdited { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    // ✅ الخصائص المحسوبة الجديدة
    public int DeliveredCount => _receipts.Count(r => r.Status >= MessageStatus.Delivered);
    public int ReadCount => _receipts.Count(r => r.Status >= MessageStatus.Read);

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

        // ✅ تعديل هنا: نضيف RoomId للـ Receipt
        foreach (var userId in recipients)
        {
            _receipts.Add(new MessageReceipt(Id, roomId, userId));
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
        {
            Console.WriteLine($"[ENTITY] NO RECEIPT found for user {userId.Value} on message {Id.Value}");
            return;
        }

        if (receipt.Status >= MessageStatus.Delivered)
        {
            Console.WriteLine($"[ENTITY] Already delivered for user {userId.Value} on message {Id.Value}");
            return;
        }

        receipt.MarkDelivered();
        Console.WriteLine($"[ENTITY] Marked delivered for user {userId.Value} on message {Id.Value}, new status: {receipt.Status}");

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

    public void Edit(string newContent)
    {
        if (IsDeleted) throw new InvalidOperationException("Cannot edit a deleted message.");

        Content = newContent;
        IsEdited = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        Content = "This message was deleted";
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
                _reactions.Remove(existing);
            }
            else
            {
                existing.UpdateType(reactionType);
            }
        }
        else
        {
            _reactions.Add(new Reaction(Id, userId, reactionType));
        }
    }

    public MessageReceiptStats GetReceiptStats()
    {
        var receipts = _receipts.ToList();
        var total = receipts.Count;
        var delivered = receipts.Count(r => r.Status >= MessageStatus.Delivered);
        var read = receipts.Count(r => r.Status >= MessageStatus.Read);

        return new MessageReceiptStats(
            totalRecipients: total,
            deliveredCount: delivered,
            readCount: read,
            deliveredUsers: receipts.Where(r => r.Status >= MessageStatus.Delivered).Select(r => r.UserId).ToList(),
            readUsers: receipts.Where(r => r.Status >= MessageStatus.Read).Select(r => r.UserId).ToList()
        );
    }
}