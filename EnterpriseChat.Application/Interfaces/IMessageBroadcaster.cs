using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IMessageBroadcaster
{
    Task BroadcastMessageAsync(MessageDto message, IEnumerable<UserId> recipients);
    Task MessageDeliveredAsync(MessageId messageId, UserId userId);
    Task MessageReadAsync(MessageId messageId, UserId userId);
    Task RoomUpdatedAsync(RoomUpdatedDto update, IEnumerable<UserId> users);

    // Rooms realtime
    Task RoomUpsertedAsync(RoomListItemDto room, IEnumerable<UserId> users);

    // Group membership realtime
    Task MemberAddedAsync(RoomId roomId, UserId memberId, string displayName, IEnumerable<UserId> users);
    Task MemberRemovedAsync(RoomId roomId, UserId memberId, IEnumerable<UserId> users);

    // ✅ NEW: leave semantics
    Task MemberLeftAsync(RoomId roomId, UserId memberId, IEnumerable<UserId> users);

    // ✅ NEW: remove room from someone’s rooms list
    Task RemovedFromRoomAsync(RoomId roomId, UserId userId);
    Task RemovedFromRoomAsync(RoomId roomId, IEnumerable<UserId> users);

    // ✅ NEW: group deleted
    Task GroupDeletedAsync(RoomId roomId, IEnumerable<UserId> users);

    // ✅ NEW: admin / ownership
    Task AdminPromotedAsync(RoomId roomId, UserId userId, IEnumerable<UserId> users);
    Task AdminDemotedAsync(RoomId roomId, UserId userId, IEnumerable<UserId> users);
    Task OwnerTransferredAsync(RoomId roomId, UserId newOwnerId, IEnumerable<UserId> users);
}
