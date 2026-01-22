using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Services;

public sealed class UserDirectoryService : IUserDirectoryService
{
    private readonly ChatDbContext _db;

    public UserDirectoryService(ChatDbContext db)
    {
        _db = db;
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
}
