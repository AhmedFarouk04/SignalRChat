using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class MessageReadRepository : IMessageReadRepository
{
    private readonly ChatDbContext _context;

    public MessageReadRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MessageReadDto>> GetMessagesAsync(
        RoomId roomId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.RoomId.Value == roomId.Value)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => new MessageReadDto
            {
                Id = m.Id.Value,
                RoomId = m.RoomId.Value,
                SenderId = m.SenderId.Value,
                Content = m.Content,
                Status = MessageStatus.Sent,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
