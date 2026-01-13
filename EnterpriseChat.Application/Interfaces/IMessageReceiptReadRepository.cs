using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IMessageReceiptReadRepository
{
    Task<IReadOnlyList<MessageReadReceiptDto>> GetReadersAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default);
}
