using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public sealed class BlockedUser
{
    public UserId BlockerId { get; private set; }
    public UserId BlockedId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BlockedUser() { }

    private BlockedUser(UserId blocker, UserId blocked)
    {
        BlockerId = blocker;
        BlockedId = blocked;
        CreatedAt = DateTime.UtcNow;
    }

    public static BlockedUser Create(UserId blocker, UserId blocked)
        => new(blocker, blocked);
}
