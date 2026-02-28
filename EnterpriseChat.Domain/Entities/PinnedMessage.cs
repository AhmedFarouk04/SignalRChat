using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public sealed class PinnedMessage
{
    public Guid Id { get; private set; }
    public RoomId RoomId { get; private set; }
    public MessageId MessageId { get; private set; }
    public DateTime PinnedAt { get; private set; }
    public DateTime? PinnedUntilUtc { get; private set; }
    public UserId PinnedByUserId { get; private set; }

    private PinnedMessage() { }

    public static PinnedMessage Create(
        RoomId roomId,
        MessageId messageId,
        UserId pinnedBy,
        TimeSpan? duration = null)
    {
        return new PinnedMessage
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            MessageId = messageId,
            PinnedAt = DateTime.UtcNow,
            PinnedUntilUtc = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null,
            PinnedByUserId = pinnedBy
        };
    }

    public bool IsExpired() =>
        PinnedUntilUtc.HasValue && PinnedUntilUtc.Value < DateTime.UtcNow;
}