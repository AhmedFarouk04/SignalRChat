using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IRoomAuthorizationService
{
    Task EnsureUserIsMemberAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default);

    Task EnsureUserIsOwnerAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default);

    Task EnsureUserIsAdminAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default);



}
