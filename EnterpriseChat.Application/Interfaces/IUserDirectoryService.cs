using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IUserDirectoryService
{
    // في IUserDirectoryService.cs
    Task<UserDto?> GetUserAsync(UserId userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserDirectoryItemDto>> SearchAsync(
           string query,
           Guid? excludeUserId,  // ✅ أضف هذا
           int take,
           CancellationToken ct = default); Task<UserSummaryDto?> GetUserSummaryAsync(UserId userId, CancellationToken ct = default);

}
