using EnterpriseChat.Domain.Common;
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

    Task<IReadOnlyList<MessageReceipt>> GetReceiptsForMessageAsync(
    MessageId messageId, 
    CancellationToken ct = default);

Task<MessageReceiptStats> GetMessageStatsAsync(
    MessageId messageId, 
    CancellationToken ct = default);

Task<IReadOnlyList<UserId>> GetReadersAsync(
    MessageId messageId, 
    CancellationToken ct = default);

Task<IReadOnlyList<UserId>> GetDeliveredUsersAsync(
    MessageId messageId, 
    CancellationToken ct = default);
    

}
