using EnterpriseChat.Application.DTOs;

namespace EnterpriseChat.Application.Interfaces;

public interface IUserDirectoryService
{
    Task<IReadOnlyList<UserDirectoryItemDto>> SearchAsync(string query, int take, CancellationToken ct = default);
}
