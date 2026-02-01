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

    public async Task<IReadOnlyList<MessageReadDto>> GetMessagesAsync(RoomId roomId, int skip, int take, CancellationToken ct)
    {
        var messages = await _context.Messages
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Include(m => m.Receipts) // مهم: Include الـ Receipts
            .Select(m => new MessageReadDto
            {
                Id = m.Id.Value,
                RoomId = m.RoomId.Value,
                SenderId = m.SenderId.Value,
                Content = m.Content,
                Status = MessageStatus.Sent, // أو calculate لو لازم
                CreatedAt = m.CreatedAt,
                Receipts = m.Receipts.Select(r => new MessageReceiptDto
                {
                    UserId = r.UserId.Value,
                    Status = r.Status // Delivered or Read
                }).ToList()
            })
            .ToListAsync(ct);

        return messages;
    }
}