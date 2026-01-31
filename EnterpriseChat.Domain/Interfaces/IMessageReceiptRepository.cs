using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Interfaces;

public interface IMessageReceiptRepository
{
    Task<MessageReceipt?> GetAsync(
        MessageId messageId,
        UserId userId,
        CancellationToken cancellationToken);

    Task AddAsync(
        MessageReceipt receipt,
        CancellationToken cancellationToken);

    Task<int> TryMarkDeliveredAsync(MessageId messageId, UserId userId, CancellationToken ct = default);

}
