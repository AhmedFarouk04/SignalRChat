using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IUserDirectoryService
{
    Task<UserDto?> GetUserAsync(UserId userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserDirectoryItemDto>> SearchAsync(
           string query,
           Guid? excludeUserId,  
           int take,
           CancellationToken ct = default); Task<UserSummaryDto?> GetUserSummaryAsync(UserId userId, CancellationToken ct = default);

}
