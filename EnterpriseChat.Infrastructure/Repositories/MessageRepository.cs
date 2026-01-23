using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class MessageRepository : IMessageRepository
{
    private readonly ChatDbContext _context;

    public MessageRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        await _context.Messages.AddAsync(message, cancellationToken);
    }

    public async Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken)
    {
        return await _context.Messages
            .FirstOrDefaultAsync(
                m => m.Id == MessageId.From(messageId),
                cancellationToken
            );
    }


    public async Task<IReadOnlyList<Message>> GetByRoomAsync(
       RoomId roomId,
       int skip,
       int take,
       CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId)   // ✅ هنا
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

}
