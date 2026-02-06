// EnterpriseChat.Domain/Interfaces/IReactionRepository.cs
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Interfaces;

public interface IReactionRepository
{
    Task<Reaction?> GetAsync(MessageId messageId, UserId userId, CancellationToken ct = default);
    Task AddAsync(Reaction reaction, CancellationToken ct = default);
    Task UpdateAsync(Reaction reaction, CancellationToken ct = default);
    Task RemoveAsync(Reaction reaction, CancellationToken ct = default);
    Task<IReadOnlyList<Reaction>> GetForMessageAsync(MessageId messageId, CancellationToken ct = default);
    Task<Dictionary<Guid, List<Reaction>>> GetForMessagesAsync(IEnumerable<MessageId> messageIds, CancellationToken ct = default);
    Task<bool> ExistsAsync(MessageId messageId, UserId userId, CancellationToken ct = default);
}