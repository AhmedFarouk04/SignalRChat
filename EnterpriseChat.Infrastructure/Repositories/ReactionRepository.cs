// EnterpriseChat.Infrastructure/Repositories/ReactionRepository.cs
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class ReactionRepository : IReactionRepository
{
    private readonly ChatDbContext _context;

    public ReactionRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<Reaction?> GetAsync(MessageId messageId, UserId userId, CancellationToken ct = default)
    {
        return await _context.Reactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId, ct);
    }

    public async Task AddAsync(Reaction reaction, CancellationToken ct = default)
    {
        await _context.Reactions.AddAsync(reaction, ct);
    }

    public Task UpdateAsync(Reaction reaction, CancellationToken ct = default)
    {
        _context.Reactions.Update(reaction);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Reaction reaction, CancellationToken ct = default)
    {
        _context.Reactions.Remove(reaction);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Reaction>> GetForMessageAsync(MessageId messageId, CancellationToken ct = default)
    {
        return await _context.Reactions
            .Where(r => r.MessageId == messageId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, List<Reaction>>> GetForMessagesAsync(
        IEnumerable<MessageId> messageIds,
        CancellationToken ct = default)
    {
        var ids = messageIds.Select(m => m.Value).ToList();
        if (!ids.Any()) return new Dictionary<Guid, List<Reaction>>();

        var reactions = await _context.Reactions
            .Where(r => ids.Contains(r.MessageId.Value))
            .ToListAsync(ct);

        return reactions
            .GroupBy(r => r.MessageId.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task<bool> ExistsAsync(MessageId messageId, UserId userId, CancellationToken ct = default)
    {
        return await _context.Reactions
            .AnyAsync(r => r.MessageId == messageId && r.UserId == userId, ct);
    }
}