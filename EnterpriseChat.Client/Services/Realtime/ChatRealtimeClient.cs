using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Ui;
using Microsoft.AspNetCore.SignalR.Client;

namespace EnterpriseChat.Client.Services.Realtime;

public sealed class ChatRealtimeClient : IChatRealtimeClient, IAsyncDisposable
{
    private readonly ITokenStore _tokenStore;
    private readonly HttpClient _http;
    private readonly RoomFlagsStore _flags;

    private HubConnection? _connection;
    private CancellationTokenSource? _typingCts;
    private readonly TimeSpan _typingDebounce = TimeSpan.FromMilliseconds(300);
    private readonly TimeSpan _typingStopTimeout = TimeSpan.FromMilliseconds(1200);
    private DateTime _lastTypingSent = DateTime.MinValue;
    private Guid? _currentRoomId;
    private bool _isDisposed = false;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
    private int _reconnectAttempts = 0;
    private readonly int _maxReconnectAttempts = 5;

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
    public event Action<Guid, Guid?>? MessagePinned;
    public event Action? Disconnected;
    public event Action? Reconnected;
    public event Action<RoomUpdatedModel>? RoomUpdated;
    public event Action<Guid, string>? GroupRenamed;
    public event Action<Guid, Guid, string>? MemberAdded;
    public event Action<Guid, Guid, string?>? MemberRemoved;
    public event Action<Guid>? GroupDeleted;
    public event Action<Guid, Guid>? AdminPromoted;
    public event Action<Guid, Guid>? AdminDemoted;
    public event Action<Guid, Guid>? OwnerTransferred;
    public event Action<RoomListItemDto>? RoomUpserted;
    public event Action<Guid, Guid, int>? MessageStatusUpdated;
    public event Action<Guid, Guid>? MessageDeliveredToAll;
    public event Action<Guid, Guid>? MessageReadToAll;
    public event Action<Guid, Guid, int, bool>? MessageReactionUpdated;
    public event Action<Guid, string>? MessageUpdated;
    public event Action<Guid>? MessageDeleted;
    public event Action<Guid, bool>? UserBlockedByMeChanged;
    public event Action<Guid, bool>? UserBlockedMeChanged;
    public event Action<Guid>? OnDemandOnlineCheckRequested;
    public event Action<Guid, int, int, int>? MessageReceiptStatsUpdated;

    public ChatRealtimeClient(ITokenStore tokenStore, HttpClient http, RoomFlagsStore flags)
    {
        _tokenStore = tokenStore;
        _http = http;
        _flags = flags;
    }

    public async Task ConnectAsync()
    {
        if (_isDisposed) return;

        await _connectionLock.WaitAsync();
        try
        {
            // ✅ لو متصل بالفعل، ارجع
            if (_connection != null && _connection.State == HubConnectionState.Connected)
            {
                Console.WriteLine("[SignalR] Already connected");
                State.IsConnected = true;
                return;
            }

            // ✅ لو في حالة توصيل، انتظر
            if (_connection != null && _connection.State == HubConnectionState.Connecting)
            {
                Console.WriteLine("[SignalR] Already connecting, waiting...");
                var startTime = DateTime.UtcNow;
                while (_connection.State == HubConnectionState.Connecting &&
                       DateTime.UtcNow - startTime < TimeSpan.FromSeconds(5))
                {
                    await Task.Delay(100);
                }
                if (_connection.State == HubConnectionState.Connected)
                {
                    State.IsConnected = true;
                    return;
                }
            }

            // ✅ تنظيف الاتصال القديم
            if (_connection != null)
            {
                try
                {
                    await _connection.DisposeAsync();
                }
                catch { }
                _connection = null;
            }

            _reconnectAttempts = 0;

            var apiBase = _http.BaseAddress?.ToString()?.TrimEnd('/') ?? "https://localhost:7188";
            var hubUrl = $"{apiBase}/hubs/chat";

            Console.WriteLine($"[SignalR] Connecting to {hubUrl}");

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await _tokenStore.GetAsync();
                        return token;
                    };
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                    options.CloseTimeout = TimeSpan.FromSeconds(5); // ✅ تقليل timeout
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.FromSeconds(1),  // ✅ أسرع
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

            RegisterHandlers();

            _connection.Closed += async (error) =>
            {
                Console.WriteLine($"[SignalR] ⚠️ Connection closed: {error?.Message}");
                State.IsConnected = false;
                Disconnected?.Invoke();

                // ✅ محاولة إعادة الاتصال بشكل محدود
                _ = Task.Run(async () =>
                {
                    await _connectionLock.WaitAsync();
                    try
                    {
                        _reconnectAttempts = 0;
                        while (_reconnectAttempts < _maxReconnectAttempts &&
                               _connection?.State != HubConnectionState.Connected &&
                               !_isDisposed)
                        {
                            try
                            {
                                _reconnectAttempts++;
                                Console.WriteLine($"[SignalR] Reconnect attempt {_reconnectAttempts}/{_maxReconnectAttempts}");

                                await Task.Delay(1000 * _reconnectAttempts); // 1s, 2s, 3s...

                                if (_connection?.State != HubConnectionState.Connected && !_isDisposed)
                                {
                                    await _connection?.StartAsync();
                                    Console.WriteLine("[SignalR] Reconnected successfully!");
                                    State.IsConnected = true;
                                    Reconnected?.Invoke();

                                    if (_currentRoomId.HasValue)
                                    {
                                        await JoinRoomAsync(_currentRoomId.Value);
                                    }
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[SignalR] Reconnect attempt {_reconnectAttempts} failed: {ex.Message}");
                            }
                        }

                        if (_reconnectAttempts >= _maxReconnectAttempts)
                        {
                            Console.WriteLine("[SignalR] Max reconnect attempts reached, giving up");
                        }
                    }
                    finally
                    {
                        _connectionLock.Release();
                    }
                });
            };

            _connection.Reconnecting += error =>
            {
                Console.WriteLine($"[SignalR] 🔄 Reconnecting: {error?.Message}");
                State.IsConnected = false;
                Disconnected?.Invoke();
                return Task.CompletedTask;
            };

            _connection.Reconnected += async id =>
            {
                Console.WriteLine($"[SignalR] ✅ Reconnected: {id}");
                State.IsConnected = true;
                _reconnectAttempts = 0;
                Reconnected?.Invoke();

                if (_currentRoomId.HasValue)
                {
                    try
                    {
                        await JoinRoomAsync(_currentRoomId.Value);
                        Console.WriteLine($"[SignalR] Re-joined room {_currentRoomId.Value}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SignalR] Failed to re-join room: {ex.Message}");
                    }
                }
            };

            await _connection.StartAsync();
            Console.WriteLine("[SignalR] ✅ Connected successfully!");
            State.IsConnected = true;
            _reconnectAttempts = 0;

            try
            {
                var onlineUsers = await _connection.InvokeAsync<List<Guid>>("GetOnlineUsers", cancellationToken: CancellationToken.None);
                State.OnlineUsers = onlineUsers ?? new List<Guid>();
                Console.WriteLine($"[SignalR] Online users loaded: {State.OnlineUsers.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] Failed to get online users: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalR] ❌ Connection failed: {ex.Message}");
            State.IsConnected = false;
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task JoinRoomAsync(Guid roomId)
    {
        _currentRoomId = roomId;

        if (_connection?.State != HubConnectionState.Connected)
        {
            await ConnectAsync();
        }

        try
        {
            await _connection!.InvokeAsync("JoinRoom", roomId.ToString());
            Console.WriteLine($"[SignalR] Joined room {roomId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalR] Failed to join room {roomId}: {ex.Message}");
        }
    }

    public async Task GroupRenamedAsync(Guid roomId, string newName)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("GroupRenamed", roomId, newName);
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
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
                _currentRoomId = null;
                _reconnectAttempts = 0;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<List<Guid>> GetOnlineUsersAsync()
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            return await _connection.InvokeAsync<List<Guid>>("GetOnlineUsers");
        }
        return new List<Guid>();
    }

    public async Task EnsureConnectedAsync()
    {
        if (_connection?.State == HubConnectionState.Connected)
            return;

        if (_connection?.State == HubConnectionState.Disconnected)
        {
            await ConnectAsync();
        }
        else
        {
            // Reconnecting or something - wait
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(10))
            {
                if (_connection?.State == HubConnectionState.Connected)
                    return;
                await Task.Delay(100);
            }
            throw new TimeoutException("Failed to connect to SignalR");
        }
    }

    public Task LeaveRoomAsync(Guid roomId)
        => _connection!.InvokeAsync("LeaveRoom", roomId.ToString());

    public Task MarkReadAsync(Guid messageId)
        => _connection!.InvokeAsync("MarkRead", messageId);

    public async Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId)
    {
        try
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("MarkRoomRead", roomId, lastMessageId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalR] MarkRoomRead failed: {ex.Message}");
            throw;
        }
    }

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
        _connection!.On<MessageDto>("MessageReceived", dto =>
        {
            Console.WriteLine($"[SignalR] 🟢 MESSAGE RECEIVED! ID: {dto.Id}");

            var message = new MessageModel
            {
                Id = dto.Id,
                RoomId = dto.RoomId,
                SenderId = dto.SenderId,
                Content = dto.Content,
                CreatedAt = dto.CreatedAt,
                Status = (Client.Models.MessageStatus)dto.Status,
                ReplyToMessageId = dto.ReplyToMessageId,
                IsEdited = dto.IsEdited,
                IsDeleted = dto.IsDeleted,
                ReadCount = dto.ReadCount,
                DeliveredCount = dto.DeliveredCount,
                TotalRecipients = dto.TotalRecipients
            };

            if (dto.ReplyInfo != null)
            {
                message.ReplyInfo = new ReplyInfoModel
                {
                    MessageId = dto.ReplyInfo.MessageId,
                    SenderId = dto.ReplyInfo.SenderId,
                    SenderName = dto.ReplyInfo.SenderName,
                    ContentPreview = dto.ReplyInfo.ContentPreview,
                    CreatedAt = dto.ReplyInfo.CreatedAt,
                    IsDeleted = dto.ReplyInfo.IsDeleted
                };
            }

            MessageReceived?.Invoke(message);
        });

        _connection!.On("Pong", () =>
        {
            Console.WriteLine("[SignalR] Pong received");
        });

        _connection.On<Guid>("MessageDelivered", id => MessageDelivered?.Invoke(id));
        _connection.On<Guid>("MessageRead", id => MessageRead?.Invoke(id));
        _connection.On<RoomUpdatedModel>("RoomUpdated", upd => RoomUpdated?.Invoke(upd));
        _connection.On<RoomListItemDto>("RoomUpserted", dto => RoomUpserted?.Invoke(dto));

        _connection.On<Guid, bool>("UserBlockedByMeChanged", (uid, blocked) =>
        {
            _flags.SetBlockedByMe(uid, blocked);
            UserBlockedByMeChanged?.Invoke(uid, blocked);
        });

        _connection!.On<Guid>("CheckUserOnline", async userId =>
        {
            Console.WriteLine($"[SignalR] CheckUserOnline requested for user {userId}");

            try
            {
                bool isOnline = await _connection.InvokeAsync<bool>("GetUserOnlineStatus", userId);
                var set = State.OnlineUsers.ToHashSet();

                if (isOnline)
                {
                    if (!set.Contains(userId))
                    {
                        set.Add(userId);
                        State.OnlineUsers = set.ToList();
                        UserOnline?.Invoke(userId);
                        Console.WriteLine($"[SignalR] User {userId} is now ONLINE");
                    }
                }
                else
                {
                    if (set.Contains(userId))
                    {
                        set.Remove(userId);
                        State.OnlineUsers = set.ToList();
                        UserOffline?.Invoke(userId);
                        Console.WriteLine($"[SignalR] User {userId} is now OFFLINE");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckUserOnline] Failed: {ex.Message}");
            }
        });

        _connection.On<Guid, Guid, int>("MessageStatusUpdated", (mId, uId, stat) =>
            MessageStatusUpdated?.Invoke(mId, uId, stat));

        _connection.On<Guid, bool>("UserBlockedMeChanged", (uid, blocked) =>
        {
            _flags.SetBlockedMe(uid, blocked);
            UserBlockedMeChanged?.Invoke(uid, blocked);
        });

        _connection.On<Guid, bool>("RoomMuteChanged", (rid, muted) =>
        {
            _flags.SetMuted(rid, muted);
            RoomMuteChanged?.Invoke(rid, muted);
        });

        _connection.On<Guid, int, int, int>("MessageReceiptStatsUpdated",
            (messageId, totalRecipients, deliveredCount, readCount) =>
            {
                MessageReceiptStatsUpdated?.Invoke(messageId, totalRecipients, deliveredCount, readCount);
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

        _connection.On<Guid, Guid>("MessageDeliveredToAll",
            (messageId, senderId) => MessageDeliveredToAll?.Invoke(messageId, senderId));

        _connection.On<Guid, Guid>("MessageReadToAll",
            (messageId, senderId) => MessageReadToAll?.Invoke(messageId, senderId));

        _connection.On<Guid, string>("GroupRenamed",
            (roomId, newName) => GroupRenamed?.Invoke(roomId, newName));

        _connection.On<Guid, Guid, string>("MemberAdded",
            (roomId, userId, displayName) => MemberAdded?.Invoke(roomId, userId, displayName));

        _connection.On<Guid, Guid, int, bool>("MessageReactionUpdated",
            (messageId, userId, reactionType, isNewReaction) =>
                MessageReactionUpdated?.Invoke(messageId, userId, reactionType, isNewReaction));

        _connection.On<Guid, Guid, string?>("MemberRemoved",
            (roomId, userId, removerName) => MemberRemoved?.Invoke(roomId, userId, removerName));

        _connection.On<Guid>("GroupDeleted", roomId => GroupDeleted?.Invoke(roomId));
        _connection.On<Guid, Guid>("AdminPromoted", (roomId, userId) => AdminPromoted?.Invoke(roomId, userId));
        _connection.On<Guid, Guid>("AdminDemoted", (roomId, userId) => AdminDemoted?.Invoke(roomId, userId));
        _connection.On<Guid, Guid>("OwnerTransferred", (roomId, newOwnerId) => OwnerTransferred?.Invoke(roomId, newOwnerId));
        _connection.On<Guid, string>("MessageUpdated", (messageId, newContent) => MessageUpdated?.Invoke(messageId, newContent));
        _connection.On<Guid>("MessageDeleted", messageId => MessageDeleted?.Invoke(messageId));
        _connection.On<Guid, Guid?>("MessagePinned", (rid, mid) => MessagePinned?.Invoke(rid, mid));
    }

    public async Task SendMessageWithReplyAsync(Guid roomId, MessageModel message)
    {
        try
        {
            if (_connection == null) return;

            var request = new
            {
                RoomId = roomId,
                Content = message.Content,
                ReplyToMessageId = message.ReplyToMessageId,
                ReplyInfo = message.ReplyInfo
            };

            await _connection.InvokeAsync("SendMessageWithReply", request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SendMessageWithReply error: {ex.Message}");
        }
    }

    public Task PinMessageAsync(Guid roomId, Guid? messageId)
        => _connection!.InvokeAsync("PinMessage", roomId, messageId);

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        await DisconnectAsync();
        _connectionLock.Dispose();
        _typingCts?.Dispose();
    }
}