using EnterpriseChat.Domain.ValueObjects;


namespace EnterpriseChat.Application.Interfaces
{
    public interface IRoomAuthorizationService
    {
        Task<bool> CanAccessAsync(
            RoomId roomId,
            UserId userId,
            CancellationToken cancellationToken = default);
    }
}
