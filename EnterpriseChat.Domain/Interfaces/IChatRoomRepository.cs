using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;

public interface IChatRoomRepository
{
    Task AddAsync(ChatRoom room, CancellationToken cancellationToken = default);

    Task<ChatRoom?> GetByIdAsync(
     RoomId roomId,
     CancellationToken cancellationToken = default);

    Task<ChatRoom?> GetByIdWithMembersAsync(
        RoomId roomId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        RoomId roomId,
        CancellationToken cancellationToken = default);

    Task<ChatRoom?> FindPrivateRoomAsync(
        UserId a,
        UserId b,
        CancellationToken ct = default);

    Task<IReadOnlyList<ChatRoom>> GetForUserAsync(
        UserId userId,
        CancellationToken cancellationToken = default);


    Task DeleteAsync(ChatRoom room, CancellationToken ct = default);

}
