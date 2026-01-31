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

    Task<List<(MessageId Id, UserId SenderId)>> GetUnreadUpToAsync(
        RoomId roomId, DateTime lastCreatedAt, UserId readerId, int take, CancellationToken ct = default);

    Task<int> BulkMarkReadUpToAsync(
        RoomId roomId, DateTime lastCreatedAt, UserId readerId, CancellationToken ct = default);

    Task<(RoomId RoomId, UserId SenderId)?> GetRoomAndSenderAsync(MessageId id, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(RoomId roomId, UserId userId, CancellationToken ct);

    // ✅ bulk methods على Guid مباشرة
    Task<Dictionary<Guid, Message?>> GetLastMessagesAsync(IEnumerable<Guid> roomIds, CancellationToken ct = default);

    Task<Dictionary<Guid, int>> GetUnreadCountsAsync(
        IEnumerable<Guid> roomIds,
        UserId userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<MessageReadInfo>> GetMessageIdsAndSendersUpToAsync(RoomId roomId, DateTime maxCreatedAt, CancellationToken ct = default);

}
