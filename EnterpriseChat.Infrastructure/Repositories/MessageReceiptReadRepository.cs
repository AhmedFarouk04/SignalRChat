using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class MessageReceiptReadRepository
    : IMessageReceiptReadRepository
{
    private readonly ChatDbContext _context;

    public MessageReceiptReadRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MessageReadReceiptDto>> GetReadersAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default)
    {
        return await _context.MessageReceipts
            .AsNoTracking()
            .Where(r =>
                r.MessageId.Value == messageId.Value &&
                r.Status == Domain.Enums.MessageStatus.Read)
            .Select(r => new MessageReadReceiptDto
            {
                UserId = r.UserId.Value,
                ReadAt = r.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
