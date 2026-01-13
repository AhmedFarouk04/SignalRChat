using EnterpriseChat.Client.Authentication;
using EnterpriseChat.Client.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace EnterpriseChat.Client.Services;

public sealed class ChatHubClient
{
    private HubConnection? _connection;
    private readonly ITokenService _tokenService;

    public event Action<Guid>? MessageDelivered;
    public event Action<Guid>? MessageRead;
    public event Action<MessageModel>? MessageReceived;
    public event Action<Guid>? UserOnline;
    public event Action<Guid>? UserOffline;
    public event Action<Guid>? UserTyping;
    public event Action<IReadOnlyCollection<Guid>>? PresenceSnapshot;

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
    }

    public Task JoinRoomAsync(Guid roomId)
        => _connection!.InvokeAsync("JoinRoom", roomId.ToString());

    public Task LeaveRoomAsync(Guid roomId)
        => _connection!.InvokeAsync("LeaveRoom", roomId.ToString());

    public Task TypingAsync(Guid roomId)
        => _connection!.InvokeAsync("Typing", roomId.ToString());

    public Task MarkReadAsync(Guid messageId)
        => _connection!.InvokeAsync("MarkRead", messageId);
}
