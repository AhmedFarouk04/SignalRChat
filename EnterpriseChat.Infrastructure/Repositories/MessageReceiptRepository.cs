using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class MessageReceiptRepository
    : IMessageReceiptRepository
{
    private readonly ChatDbContext _context;

    public MessageReceiptRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<MessageReceipt?> GetAsync(
        MessageId messageId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        return await _context.MessageReceipts
            .FirstOrDefaultAsync(
                x =>
                    x.MessageId == messageId &&
                    x.UserId == userId,
                cancellationToken);
    }

    public async Task AddAsync(
        MessageReceipt receipt,
        CancellationToken cancellationToken)
    {
        await _context.MessageReceipts
            .AddAsync(receipt, cancellationToken);
    }
    public async Task<int> TryMarkDeliveredAsync(MessageId messageId, UserId userId, CancellationToken ct = default)
    {
        return await _context.MessageReceipts
            .Where(r => r.MessageId == messageId && r.UserId == userId && r.Status < MessageStatus.Delivered)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, MessageStatus.Delivered)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow),
            ct);
    }

}
