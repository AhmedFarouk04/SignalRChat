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
    Task MemberRemovedAsync(RoomId roomId, UserId memberId, IEnumerable<UserId> users);
    Task MemberLeftAsync(RoomId roomId, UserId memberId, IEnumerable<UserId> users);

    // ✅ NEW: remove room from someone’s rooms list
    Task RemovedFromRoomAsync(RoomId roomId, UserId userId);
    Task BroadcastToRoomGroupAsync(Guid roomId, MessageDto message);

    Task RemovedFromRoomAsync(RoomId roomId, IEnumerable<UserId> users);

    // ✅ NEW: group deleted
    Task GroupDeletedAsync(RoomId roomId, IEnumerable<UserId> users);

    // ✅ NEW: admin / ownership
    Task AdminPromotedAsync(RoomId roomId, UserId userId, IEnumerable<UserId> users);
    Task AdminDemotedAsync(RoomId roomId, UserId userId, IEnumerable<UserId> users);
    Task OwnerTransferredAsync(RoomId roomId, UserId newOwnerId, IEnumerable<UserId> users);

    // ✅ تعديل: نرسل roomId بدلاً من targetUserId (للبث للغرفة)
    Task MessageReceiptStatsUpdatedAsync(
    Guid messageId,
    Guid roomId,
    int totalRecipients,
    int deliveredCount,
    int readCount);


    Task MessageUpdatedAsync(MessageId messageId, string newContent, IEnumerable<UserId> recipients);
    Task MessageDeletedAsync(MessageId messageId, IEnumerable<UserId> recipients);

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

    Task MessageReactionUpdatedAsync(
        MessageId messageId,
        UserId userId,
        ReactionType reactionType,
        bool isNewReaction,
        IEnumerable<UserId> roomMembers);

    Task NotifyMessagePinned(Guid roomId, Guid? messageId);
}