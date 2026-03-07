using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Models;
using System.Threading.Tasks;

namespace EnterpriseChat.Client.Services.Realtime;

public interface IChatRealtimeClient
{
    ChatRealtimeState State { get; }

    event Action<MessageModel>? MessageReceived;

    event Action<RoomUpdatedModel>? RoomUpdated;
    event Action<Guid>? OnDemandOnlineCheckRequested;
    event Action<Guid>? UserOnline;
    event Action<Guid>? UserOffline;
    event Action<Guid, DateTime>? UserLastSeenUpdated;
    event Action<Guid, int>? RoomPresenceUpdated;
        event Action<Guid>? ChatDeleted;
    event Action<Guid, bool>? ChatCleared;
    event Action<Guid, Guid>? TypingStarted;
    event Action<Guid, Guid>? TypingStopped;

    event Action<Guid>? RemovedFromRoom;

    event Action? Disconnected;
    event Action? Reconnected;
    event Action<Guid, bool>? UserBlockedByMeChanged;
    event Action<Guid, bool>? UserBlockedMeChanged;
    event Action<Guid, bool>? RoomMuteChanged;

    event Action<Guid, string>? GroupRenamed;
    event Action<Guid, Guid, string>? MemberAdded;
    event Action<Guid, Guid, string?>? MemberRemoved; 
    event Action<Guid>? GroupDeleted;
    event Action<Guid, Guid>? AdminPromoted;
    event Action<Guid, Guid>? AdminDemoted;
    event Action<Guid, Guid>? OwnerTransferred;
    event Action<RoomListItemDto>? RoomUpserted;
    event Action<Guid, string>? MessageUpdated;     public event Action<Guid, bool>? MessageDeleted;

        event Action<Guid, Guid, int> MessageStatusUpdated;     event Action<Guid, Guid, bool>? MemberRoleChanged;
    event Action<Guid, Guid, int, bool> MessageReactionUpdated;     event Action<Guid, Guid?>? MessagePinned;      event Action<List<Guid>>? InitialOnlineUsersReceived;

    event Action<Guid, Guid, int, int, int>? MessageReceiptStatsUpdated;
    event Action<Guid, Guid>? MessageDelivered;      event Action<Guid, Guid>? MessageRead;           event Action<Guid, Guid, Guid>? MessageDeliveredToAll;     event Action<Guid, Guid, Guid>? MessageReadToAll;      
        event Action<Guid, List<Guid>>? InitialTypingUsersReceived;

        IReadOnlyList<Guid> GetTypingUsersInRoom(Guid roomId);
    Task PinMessageAsync(Guid roomId, Guid? messageId);
    Task SendMessageWithReplyAsync(Guid roomId, MessageModel message);
    Task StopTypingImmediatelyAsync(Guid roomId);
    Task ConnectAsync();
    Task DisconnectAsync(bool force = false);
        Task NotifyMemberRoleChangedAsync(Guid roomId, Guid userId, bool isAdmin);
    Task JoinRoomAsync(Guid roomId);
    Task LeaveRoomAsync(Guid roomId);

    Task MarkReadAsync(Guid messageId);
    Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId);
    Task GroupRenamedAsync(Guid roomId, string newName);
    Task NotifyTypingAsync(Guid roomId);
    Task<bool> CheckUserOnlineStatus(Guid userId);
    Task<object> GetUserOnlineStatus(Guid userId);
    Task<List<Guid>> GetOnlineUsersAsync(); 

}
