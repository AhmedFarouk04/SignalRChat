using EnterpriseChat.Domain.Common;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
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
          .Where(r => r.MessageId == messageId && r.UserId == userId && r.Status < MessageStatus.Delivered && r.Status != MessageStatus.Failed).ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, MessageStatus.Delivered)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow),
            ct);
    }

    public async Task<IReadOnlyList<MessageReceipt>> GetReceiptsForMessageAsync(
      MessageId messageId,
      CancellationToken ct = default)
    {
        return await _context.MessageReceipts
            .Where(r => r.MessageId == messageId)
            .ToListAsync(ct);
    }

    public async Task<MessageReceiptStats> GetMessageStatsAsync(
        MessageId messageId,
        CancellationToken ct = default)
    {
        var receipts = await GetReceiptsForMessageAsync(messageId, ct);

        var deliveredUsers = receipts
            .Where(r => r.Status >= MessageStatus.Delivered)
            .Select(r => r.UserId)
            .ToList();

        var readUsers = receipts
            .Where(r => r.Status >= MessageStatus.Read)
            .Select(r => r.UserId)
            .ToList();

        return new MessageReceiptStats(
            totalRecipients: receipts.Count,
            deliveredCount: deliveredUsers.Count,
            readCount: readUsers.Count,
            deliveredUsers: deliveredUsers,
            readUsers: readUsers
        );
    }

    public async Task<IReadOnlyList<UserId>> GetReadersAsync(
        MessageId messageId,
        CancellationToken ct = default)
    {
        return await _context.MessageReceipts
            .Where(r => r.MessageId == messageId && r.Status >= MessageStatus.Read)
            .Select(r => r.UserId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserId>> GetDeliveredUsersAsync(
        MessageId messageId,
        CancellationToken ct = default)
    {
        return await _context.MessageReceipts
            .Where(r => r.MessageId == messageId && r.Status >= MessageStatus.Delivered)
            .Select(r => r.UserId)
            .ToListAsync(ct);
    }


}
