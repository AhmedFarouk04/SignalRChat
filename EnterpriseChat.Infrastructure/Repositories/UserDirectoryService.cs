using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Services;

public sealed class UserDirectoryService : IUserDirectoryService
{
    private readonly ChatDbContext _db;
    private readonly IPresenceService _presence;

    public UserDirectoryService(ChatDbContext db, IPresenceService presence)
    {
        _db = db;
        _presence = presence;
    }

    public async Task<IReadOnlyList<UserDirectoryItemDto>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        query = query.Trim();
        take = Math.Clamp(take, 1, 50);

        return await _db.Users
            .AsNoTracking()
            .Where(u =>
                u.DisplayName.Contains(query) ||
                (u.Email != null && u.Email.Contains(query)))
            .OrderBy(u => u.DisplayName)
            .Take(take)
            .Select(u => new UserDirectoryItemDto(u.Id, u.DisplayName, u.Email))
            .ToListAsync(ct);
    }
    // في UserDirectoryService.cs
    public async Task<UserSummaryDto?> GetUserSummaryAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (user == null) return null;

        return new UserSummaryDto
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            IsOnline = await _presence.IsOnlineAsync(userId),
            LastSeen = null
        };
    }
    public async Task<UserDto?> GetUserAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsOnline = await _presence.IsOnlineAsync(userId),
            LastSeen = null
        };
    }
}