using EnterpriseChat.Client.Authentication;
using EnterpriseChat.Client.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace EnterpriseChat.Client.Services;

public sealed class ChatHubClient
{
    private HubConnection? _connection;
    private readonly ITokenService _tokenService;
    private CancellationTokenSource? _typingCts;
    private readonly TimeSpan _typingDebounce = TimeSpan.FromMilliseconds(300);
    private readonly TimeSpan _typingStopTimeout = TimeSpan.FromMilliseconds(1200);
    private DateTime _lastTypingSent = DateTime.MinValue;


    public event Action<Guid>? MessageDelivered;
    public event Action<Guid>? MessageRead;
    public event Action<MessageModel>? MessageReceived;
    public event Action<Guid>? UserOnline;
    public event Action<Guid>? UserOffline;
    public event Action<Guid>? UserTyping;
    public event Action<IReadOnlyCollection<Guid>>? PresenceSnapshot;
    public event Action<Guid, Guid>? TypingStarted; // (roomId, userId)
    public event Action<Guid, Guid>? TypingStopped;
    public event Action<Guid, int>? RoomPresenceUpdated;
    public ChatHubClient(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task ConnectAsync()
    {
        if (_connection != null)
            return;

        _connection = new HubConnectionBuilder()
            .WithUrl("https://localhost:5001/hubs/chat", options =>
            {
                options.AccessTokenProvider = _tokenService.GetTokenAsync;
            })
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers();
        await _connection.StartAsync();

        var onlineUsers =
            await _connection.InvokeAsync<List<Guid>>("GetOnlineUsers");

        PresenceSnapshot?.Invoke(onlineUsers);
    }

    private void RegisterHandlers()
    {
        _connection!.On<MessageModel>("MessageReceived",
            msg => MessageReceived?.Invoke(msg));

        _connection.On<Guid>("MessageDelivered",
            id => MessageDelivered?.Invoke(id));

        _connection.On<Guid>("MessageRead",
            id => MessageRead?.Invoke(id));

        _connection.On<Guid>("UserOnline",
            id => UserOnline?.Invoke(id));

        _connection.On<Guid>("UserOffline",
            id => UserOffline?.Invoke(id));

        _connection.On<Guid>("UserTyping",
            id => UserTyping?.Invoke(id));

        _connection.On<Guid, int>("RoomPresenceUpdated",
    (roomId, count) => RoomPresenceUpdated?.Invoke(roomId, count));

        _connection.On<Guid, Guid>("TypingStarted",
            (roomId, userId) => TypingStarted?.Invoke(roomId, userId));

        _connection.On<Guid, Guid>("TypingStopped",
            (roomId, userId) => TypingStopped?.Invoke(roomId, userId));
    }

    public Task JoinRoomAsync(Guid roomId)
        => _connection!.InvokeAsync("JoinRoom", roomId.ToString());

    public Task LeaveRoomAsync(Guid roomId)
        => _connection!.InvokeAsync("LeaveRoom", roomId.ToString());

    public Task TypingAsync(Guid roomId)
        => _connection!.InvokeAsync("Typing", roomId.ToString());

    public Task MarkReadAsync(Guid messageId)
        => _connection!.InvokeAsync("MarkRead", messageId);


    public async Task NotifyTypingAsync(Guid roomId)
    {
        if (_connection is null) return;

        // debounce: ابعت start كل 300ms فقط
        var now = DateTime.UtcNow;
        if (now - _lastTypingSent > _typingDebounce)
        {
            _lastTypingSent = now;
            await _connection.InvokeAsync("TypingStart", roomId.ToString());
        }

        // reset stop timer
        _typingCts?.Cancel();
        _typingCts = new CancellationTokenSource();
        var token = _typingCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_typingStopTimeout, token);
                await _connection.InvokeAsync("TypingStop", roomId.ToString());
            }
            catch (TaskCanceledException) { }
        }, token);
    }
}
