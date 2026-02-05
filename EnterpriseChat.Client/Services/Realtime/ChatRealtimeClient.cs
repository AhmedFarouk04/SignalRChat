using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Ui;
using Microsoft.AspNetCore.SignalR.Client;
using static System.Net.WebRequestMethods;

namespace EnterpriseChat.Client.Services.Realtime;

public sealed class ChatRealtimeClient : IChatRealtimeClient
{
    private readonly ITokenStore _tokenStore;
    private readonly HttpClient _http;
    private readonly RoomFlagsStore _flags;

    private HubConnection? _connection;

    private CancellationTokenSource? _typingCts;
    private readonly TimeSpan _typingDebounce = TimeSpan.FromMilliseconds(300);
    private readonly TimeSpan _typingStopTimeout = TimeSpan.FromMilliseconds(1200);
    private DateTime _lastTypingSent = DateTime.MinValue;

    public ChatRealtimeState State { get; } = new();

    public event Action<Guid>? MessageDelivered;
    public event Action<Guid>? MessageRead;
    public event Action<MessageModel>? MessageReceived;
    public event Action<Guid, bool>? RoomMuteChanged;

    public event Action<Guid>? UserOnline;
    public event Action<Guid>? UserOffline;

    public event Action<Guid, int>? RoomPresenceUpdated;

    public event Action<Guid, Guid>? TypingStarted;
    public event Action<Guid, Guid>? TypingStopped;

    public event Action<Guid>? RemovedFromRoom;

    public event Action? Disconnected;
    public event Action? Reconnected;
    public event Action<RoomUpdatedModel>? RoomUpdated;
    public event Action<Guid, bool>? UserBlockChanged;
    public event Action<Guid, string>? GroupRenamed;
    public event Action<Guid, Guid, string>? MemberAdded;
    public event Action<Guid, Guid, string?>? MemberRemoved;
    public event Action<Guid>? GroupDeleted;
    public event Action<Guid, Guid>? AdminPromoted;
    public event Action<Guid, Guid>? AdminDemoted;
    public event Action<Guid, Guid>? OwnerTransferred;
    public event Action<RoomListItemDto>? RoomUpserted;

    public ChatRealtimeClient(ITokenStore tokenStore, HttpClient http, RoomFlagsStore flags)
    {
        _tokenStore = tokenStore;
        _http = http;
        _flags = flags;
    }

    public async Task ConnectAsync()
    {
        if (_connection != null)
            return;

        var apiBase = _http.BaseAddress?.ToString()?.TrimEnd('/') ?? "https://localhost:5001";
        var hubUrl = $"{apiBase}/hubs/chat";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = _tokenStore.GetAsync;
            })
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers();

        _connection.Reconnecting += _ =>
        {
            State.IsConnected = false;
            Disconnected?.Invoke();
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            State.IsConnected = true;
            Reconnected?.Invoke();
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            State.IsConnected = false;
            Disconnected?.Invoke();
            return Task.CompletedTask;
        };

        await _connection.StartAsync();
        State.IsConnected = true;

        var onlineUsers = await _connection.InvokeAsync<List<Guid>>("GetOnlineUsers");
        State.OnlineUsers = onlineUsers;
    }

    public async Task DisconnectAsync()
    {
        if (_connection == null)
            return;

        try
        {
            await _connection.StopAsync();
        }
        finally
        {
            await _connection.DisposeAsync();
            _connection = null;
            State.IsConnected = false;
            State.OnlineUsers = Array.Empty<Guid>();
        }
    }

    public Task JoinRoomAsync(Guid roomId)
        => _connection!.InvokeAsync("JoinRoom", roomId.ToString());

    public Task LeaveRoomAsync(Guid roomId)
        => _connection!.InvokeAsync("LeaveRoom", roomId.ToString());

    public Task MarkReadAsync(Guid messageId)
        => _connection!.InvokeAsync("MarkRead", messageId);

    public Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId)
        => _connection!.InvokeAsync("MarkRoomRead", roomId, lastMessageId);

    public async Task NotifyTypingAsync(Guid roomId)
    {
        if (_connection is null)
            return;

        var now = DateTime.UtcNow;
        if (now - _lastTypingSent > _typingDebounce)
        {
            _lastTypingSent = now;
            await _connection.InvokeAsync("TypingStart", roomId.ToString());
        }

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

    private void RegisterHandlers()
    {
        _connection!.On<MessageModel>("MessageReceived", msg => MessageReceived?.Invoke(msg));


        _connection.On<Guid>("MessageDelivered", id => MessageDelivered?.Invoke(id));
        _connection.On<Guid>("MessageRead", id => MessageRead?.Invoke(id));

        _connection.On<RoomUpdatedModel>("RoomUpdated", upd => RoomUpdated?.Invoke(upd));

        _connection.On<RoomListItemDto>("RoomUpserted", dto =>
      RoomUpserted?.Invoke(dto));


        _connection.On<Guid, bool>("RoomMuteChanged", (rid, muted) =>
        {
            _flags.SetMuted(rid, muted);
            RoomMuteChanged?.Invoke(rid, muted);
        });

        _connection.On<Guid, bool>("UserBlockChanged", (uid, blocked) =>
        {
            _flags.SetBlocked(uid, blocked);
            UserBlockChanged?.Invoke(uid, blocked);
        });

        _connection.On<Guid>("UserOnline", id =>
        {
            var set = State.OnlineUsers.ToHashSet();
            set.Add(id);
            State.OnlineUsers = set;
            UserOnline?.Invoke(id);
        });

        _connection.On<Guid>("UserOffline", id =>
        {
            var set = State.OnlineUsers.ToHashSet();
            set.Remove(id);
            State.OnlineUsers = set;
            UserOffline?.Invoke(id);
        });

        _connection.On<Guid, int>("RoomPresenceUpdated",
            (roomId, count) => RoomPresenceUpdated?.Invoke(roomId, count));

        _connection.On<Guid, Guid>("TypingStarted",
            (roomId, userId) => TypingStarted?.Invoke(roomId, userId));

        _connection.On<Guid, Guid>("TypingStopped",
            (roomId, userId) => TypingStopped?.Invoke(roomId, userId));

        _connection.On<Guid>("RemovedFromRoom",
            roomId => RemovedFromRoom?.Invoke(roomId));
      

        _connection.On<Guid, string>("GroupRenamed", (roomId, newName) => GroupRenamed?.Invoke(roomId, newName));
        _connection.On<Guid, Guid, string>("MemberAdded",
     (roomId, userId, displayName) => MemberAdded?.Invoke(roomId, userId, displayName));


        _connection.On<Guid, Guid, string?>("MemberRemoved",
    (roomId, userId, removerName) => MemberRemoved?.Invoke(roomId, userId, removerName)); _connection.On<Guid>("GroupDeleted", roomId => GroupDeleted?.Invoke(roomId));
        _connection.On<Guid, Guid>("AdminPromoted", (roomId, userId) => AdminPromoted?.Invoke(roomId, userId));
        _connection.On<Guid, Guid>("AdminDemoted", (roomId, userId) => AdminDemoted?.Invoke(roomId, userId));
        _connection.On<Guid, Guid>("OwnerTransferred", (roomId, newOwnerId) => OwnerTransferred?.Invoke(roomId, newOwnerId));
    }
}
