using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Services;

public sealed class UserLookupService : IUserLookupService
{
    private readonly ChatDbContext _db;
    public UserLookupService(ChatDbContext db) => _db = db;

    public async Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct);
    }
}
