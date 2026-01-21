namespace EnterpriseChat.Client.Services.Realtime;

public sealed class ChatRealtimeState
{
    public bool IsConnected { get; internal set; }
    public IReadOnlyCollection<Guid> OnlineUsers { get; internal set; } = Array.Empty<Guid>();
}
