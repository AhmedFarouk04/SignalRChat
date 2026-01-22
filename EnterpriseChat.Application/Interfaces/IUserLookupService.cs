namespace EnterpriseChat.Application.Interfaces;

public interface IUserLookupService
{
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct = default);
}
