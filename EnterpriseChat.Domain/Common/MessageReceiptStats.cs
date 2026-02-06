using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Common;

public sealed class MessageReceiptStats
{
    public int TotalRecipients { get; }
    public int DeliveredCount { get; }
    public int ReadCount { get; }
    public IReadOnlyList<UserId> DeliveredUsers { get; }
    public IReadOnlyList<UserId> ReadUsers { get; }

    public MessageReceiptStats(
        int totalRecipients,
        int deliveredCount,
        int readCount,
        IEnumerable<UserId> deliveredUsers,
        IEnumerable<UserId> readUsers)
    {
        TotalRecipients = totalRecipients;
        DeliveredCount = deliveredCount;
        ReadCount = readCount;
        DeliveredUsers = deliveredUsers.ToList();
        ReadUsers = readUsers.ToList();
    }

    public double DeliveredPercentage => TotalRecipients > 0
        ? (double)DeliveredCount / TotalRecipients * 100
        : 0;

    public double ReadPercentage => TotalRecipients > 0
        ? (double)ReadCount / TotalRecipients * 100
        : 0;
}