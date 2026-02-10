using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Models;

namespace EnterpriseChat.Client.Services.Realtime;

public interface IChatRealtimeClient
{
    ChatRealtimeState State { get; }

    event Action<Guid>? MessageDelivered;
    event Action<Guid>? MessageRead;
    event Action<MessageModel>? MessageReceived;

    event Action<RoomUpdatedModel>? RoomUpdated;

    event Action<Guid>? UserOnline;
    event Action<Guid>? UserOffline;

    event Action<Guid, int>? RoomPresenceUpdated;

    event Action<Guid, Guid>? TypingStarted;
    event Action<Guid, Guid>? TypingStopped;

    event Action<Guid>? RemovedFromRoom;

    event Action? Disconnected;
    event Action? Reconnected;

    event Action<Guid, bool>? RoomMuteChanged;
    event Action<Guid, bool>? UserBlockChanged;

    event Action<Guid, string>? GroupRenamed;
    event Action<Guid, Guid, string>? MemberAdded;
    event Action<Guid, Guid, string?>? MemberRemoved; 
    event Action<Guid>? GroupDeleted;
    event Action<Guid, Guid>? AdminPromoted;
    event Action<Guid, Guid>? AdminDemoted;
    event Action<Guid, Guid>? OwnerTransferred;
    event Action<RoomListItemDto>? RoomUpserted;
    event Action<Guid, string>? MessageUpdated; // (messageId, newContent)
    event Action<Guid>? MessageDeleted;
    // في IChatRealtimeClient.cs أضف:
    event Action<Guid, Guid, int> MessageStatusUpdated; // messageId, userId, status
    event Action<Guid, Guid> MessageDeliveredToAll; // messageId, senderId
    event Action<Guid, Guid> MessageReadToAll; // messageId, senderId
    event Action<Guid, Guid, int, bool> MessageReactionUpdated; // messageId, userId, reactionType, isNewReaction
    event Action<Guid, Guid?>? MessagePinned; // (roomId, messageId)
    Task PinMessageAsync(Guid roomId, Guid? messageId);
    Task SendMessageWithReplyAsync(Guid roomId, MessageModel message);

    Task ConnectAsync();
    Task DisconnectAsync();

    Task JoinRoomAsync(Guid roomId);
    Task LeaveRoomAsync(Guid roomId);

    Task MarkReadAsync(Guid messageId);
    Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId);

    Task NotifyTypingAsync(Guid roomId);



}
