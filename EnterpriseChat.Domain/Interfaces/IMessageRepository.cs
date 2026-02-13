using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Domain.Common;

public interface IMessageRepository
{
    Task AddAsync(Message message, CancellationToken cancellationToken = default);

    Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetByRoomAsync(RoomId roomId, int skip, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetByRoomForUpdateAsync(RoomId roomId, int skip, int take, CancellationToken cancellationToken = default);

    Task<DateTime?> GetCreatedAtAsync(MessageId messageId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(RoomId roomId, DateTime lastReadAt, UserId userId, CancellationToken ct = default);

    Task<int> GetTotalUnreadCountAsync(RoomId roomId, UserId userId, CancellationToken ct = default);
    Task<List<(MessageId Id, UserId SenderId)>> GetUnreadUpToAsync(
        RoomId roomId, DateTime lastCreatedAt, UserId readerId, int take, CancellationToken ct = default);

    Task<int> BulkMarkReadUpToAsync(
        RoomId roomId, DateTime lastCreatedAt, UserId readerId, CancellationToken ct = default);

    Task<(RoomId RoomId, UserId SenderId)?> GetRoomAndSenderAsync(MessageId id, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(RoomId roomId, UserId userId, CancellationToken ct);

    // ✅ bulk methods على Guid مباشرة

    Task<Dictionary<Guid, int>> GetUnreadCountsAsync(
        IEnumerable<Guid> roomIds,
        UserId userId,
        CancellationToken ct = default);
    Task<Dictionary<Guid, LastMessageInfo>> GetLastMessagesAsync(
    IReadOnlyList<Guid> roomIds,
    CancellationToken ct);


    Task<IReadOnlyList<UserId>> GetRoomMemberIdsAsync(
    RoomId roomId,
    CancellationToken ct = default);

    Task<IEnumerable<(Guid MessageId, Guid SenderId)>> GetMessageIdsAndSendersUpToAsync(
    RoomId roomId,
    DateTime upTo,
    CancellationToken ct = default);


}
