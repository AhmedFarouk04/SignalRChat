using EnterpriseChat.Client.Models;

namespace EnterpriseChat.Client.Services.Realtime;

public interface IChatRealtimeClient
{
    ChatRealtimeState State { get; }

    event Action<Guid>? MessageDelivered;
    event Action<Guid>? MessageRead;
    event Action<MessageModel>? MessageReceived;

    event Action<Guid>? UserOnline;
    event Action<Guid>? UserOffline;

    event Action<Guid, int>? RoomPresenceUpdated;

    event Action<Guid, Guid>? TypingStarted;
    event Action<Guid, Guid>? TypingStopped;

    event Action<Guid>? RemovedFromRoom;

    event Action? Disconnected;
    event Action? Reconnected;

    Task ConnectAsync();
    Task DisconnectAsync();

    Task JoinRoomAsync(Guid roomId);
    Task LeaveRoomAsync(Guid roomId);

    Task MarkReadAsync(Guid messageId);
    Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId);

    Task NotifyTypingAsync(Guid roomId);
}
