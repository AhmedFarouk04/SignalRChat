using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Enums;
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
    Task MemberRemovedAsync(RoomId roomId, UserId memberId, UserId? removerId, string? removerName, IEnumerable<UserId> users);
    // ✅ NEW: leave semantics

    // حط بجانب Method القديمة
    Task MemberRemovedAsync(RoomId roomId, UserId memberId, IEnumerable<UserId> users); // keep old
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

    // في IMessageBroadcaster.cs
    Task MessageStatusUpdatedAsync(
        MessageId messageId,
        UserId userId,
        MessageStatus newStatus,
        IEnumerable<UserId> roomMembers);

    Task MessageDeliveredToAllAsync(
        MessageId messageId,
        UserId senderId,
        IEnumerable<UserId> roomMembers);

    Task MessageReadToAllAsync(
        MessageId messageId,
        UserId senderId,
        IEnumerable<UserId> roomMembers);

    // في IMessageBroadcaster.cs أضف:
    Task MessageReactionUpdatedAsync(
        MessageId messageId,
        UserId userId,
        ReactionType reactionType,
        bool isNewReaction,
        IEnumerable<UserId> roomMembers);
}
