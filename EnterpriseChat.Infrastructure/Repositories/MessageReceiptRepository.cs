using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
                    x.MessageId.Value == messageId.Value &&
                    x.UserId.Value == userId.Value,
                cancellationToken);
    }

    public async Task AddAsync(
        MessageReceipt receipt,
        CancellationToken cancellationToken)
    {
        await _context.MessageReceipts
            .AddAsync(receipt, cancellationToken);
    }
}
