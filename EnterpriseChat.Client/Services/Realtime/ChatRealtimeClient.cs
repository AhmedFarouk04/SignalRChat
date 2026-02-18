using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Ui;
using Microsoft.AspNetCore.SignalR.Client;
using EnterpriseChat.Application.DTOs;

namespace EnterpriseChat.Client.Services.Realtime;

public sealed class ChatRealtimeClient : IChatRealtimeClient, IAsyncDisposable
{
    private readonly ITokenStore _tokenStore;
    private readonly HttpClient _http;
    private readonly RoomFlagsStore _flags;
    private readonly ICurrentUser _currentUser;
    private Guid? _cachedUserId; // Cache للـ UserId

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
    private Timer? _heartbeatTimer;
    private DateTime _lastPong = DateTime.UtcNow;
    private DateTime _lastUserOnlineEvent = DateTime.MinValue;
    private DateTime _lastUserOfflineEvent = DateTime.MinValue;
    private readonly TimeSpan _eventThrottle = TimeSpan.FromSeconds(2);
    private int _failedPings = 0;
    private readonly int _maxFailedPings = 3;
    private bool _heartbeatActive;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(90);

    public ChatRealtimeState State { get; } = new();

    public event Action<MessageModel>? MessageReceived;
    public event Action<Guid, bool>? RoomMuteChanged;
    public event Action<Guid>? UserOnline;
    public event Action<Guid>? UserOffline;
    public event Action<Guid, DateTime>? UserLastSeenUpdated;
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
    public event Action<Guid, Guid, int, bool>? MessageReactionUpdated;
    public event Action<Guid, string>? MessageUpdated;
    public event Action<Guid>? MessageDeleted;
    public event Action<Guid, bool>? UserBlockedByMeChanged;
    public event Action<Guid, bool>? UserBlockedMeChanged;
    public event Action<Guid>? OnDemandOnlineCheckRequested;
    public event Action<Guid, Guid, int, int, int>? MessageReceiptStatsUpdated;
    public event Action<List<Guid>>? InitialOnlineUsersReceived;
    public event Action<Guid, Guid>? MessageDelivered;
    public event Action<Guid, Guid>? MessageRead;
    public event Action<Guid, Guid, Guid>? MessageDeliveredToAll;
    public event Action<Guid, Guid, Guid>? MessageReadToAll;

    public ChatRealtimeClient(
        ITokenStore tokenStore,
        HttpClient http,
        RoomFlagsStore flags,
        ICurrentUser currentUser)
    {
        _tokenStore = tokenStore;
        _http = http;
        _flags = flags;
        _currentUser = currentUser;
    }

    // دالة مساعدة لجلب الـ UserId الحالي بشكل آمن
    private async Task<Guid?> GetCurrentUserIdAsync()
    {
        try
        {
            if (_cachedUserId.HasValue && _cachedUserId.Value != Guid.Empty)
                return _cachedUserId;

            var userId = await _currentUser.GetUserIdAsync();
            _cachedUserId = userId;
            return userId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetCurrentUserIdAsync] Error: {ex.Message}");
            return null;
        }
    }

    // دالة مساعدة للتحقق من حالة البلوك
    private bool IsUserBlocked(Guid userId)
    {
        return _flags.GetBlockedByMe(userId) || _flags.GetBlockedMe(userId);
    }

    public async Task ConnectAsync()
    {
        if (_isDisposed)
        {
            Console.WriteLine("[SignalR] Cannot connect, already disposed");
            return;
        }

        await _connectionLock.WaitAsync();
        try
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected)
            {
                Console.WriteLine("[SignalR] Already connected");
                State.IsConnected = true;
                return;
            }

            if (_connection != null && _connection.State == HubConnectionState.Connecting)
            {
                Console.WriteLine("[SignalR] Already connecting, waiting...");
                var startTime = DateTime.UtcNow;
                while (_connection.State == HubConnectionState.Connecting &&
                       DateTime.UtcNow - startTime < TimeSpan.FromSeconds(5))
                {
                    await Task.Delay(100);
                    if (_isDisposed) return;
                }
                if (_connection.State == HubConnectionState.Connected)
                {
                    State.IsConnected = true;
                    return;
                }
            }

            if (_connection != null)
            {
                try
                {
                    Console.WriteLine("[SignalR] Disposing old connection...");
                    await _connection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR] Error disposing old connection: {ex.Message}");
                }
                finally
                {
                    _connection = null;
                }
            }

            _reconnectAttempts = 0;
            _failedPings = 0;

            var apiBase = _http.BaseAddress?.ToString()?.TrimEnd('/') ?? "https://localhost:7188";
            var hubUrl = $"{apiBase}/hubs/chat";

            Console.WriteLine($"[SignalR] Connecting to {hubUrl}");

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        try
                        {
                            return await _tokenStore.GetAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SignalR] Error getting token: {ex.Message}");
                            return null;
                        }
                    };
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                    options.CloseTimeout = TimeSpan.FromSeconds(5);
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

            RegisterHandlers();

            _connection.Closed += async (error) =>
            {
                if (_isDisposed) return;

                Console.WriteLine($"[SignalR] ⚠️ Connection closed: {error?.Message}");
                State.IsConnected = false;
                _failedPings = 0;

                // ✅ NEW: clear presence source of truth فورًا
                State.OnlineUsers = new List<Guid>();

                try { Disconnected?.Invoke(); }
                catch (Exception ex) { Console.WriteLine($"[SignalR] Error invoking Disconnected event: {ex.Message}"); }
            };

            _connection.Reconnecting += error =>
            {
                if (_isDisposed) return Task.CompletedTask;

                Console.WriteLine($"[SignalR] 🔄 Reconnecting: {error?.Message}");
                State.IsConnected = false;
                _failedPings = 0;

                // ✅ NEW
                State.OnlineUsers = new List<Guid>();

                try { Disconnected?.Invoke(); }
                catch (Exception ex) { Console.WriteLine($"[SignalR] Error invoking Disconnected event: {ex.Message}"); }

                return Task.CompletedTask;
            };

            _connection.Reconnected += async id =>
            {
                if (_isDisposed) return;

                Console.WriteLine($"[SignalR] ✅ Reconnected: {id}");
                State.IsConnected = true;
                _reconnectAttempts = 0;
                _failedPings = 0;

                try
                {
                    var onlineUsers = await _connection.InvokeAsync<List<Guid>>("GetOnlineUsers", CancellationToken.None);

                    // ✅ NEW: فلترة blocked
                    State.OnlineUsers = (onlineUsers ?? new List<Guid>())
                        .Where(uid => !_flags.GetBlockedByMe(uid) && !_flags.GetBlockedMe(uid))
                        .ToList();

                    Console.WriteLine($"[SignalR] Online users loaded after reconnect: {State.OnlineUsers.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR] Failed to get online users after reconnect: {ex.Message}");
                    // ✅ لو فشل snapshot، نخليها فاضية بدل stale
                    State.OnlineUsers = new List<Guid>();
                }

                if (_currentRoomId.HasValue)
                {
                    try { await JoinRoomAsync(_currentRoomId.Value); }
                    catch (Exception ex) { Console.WriteLine($"[SignalR] Failed to re-join room: {ex.Message}"); }
                }

                try { Reconnected?.Invoke(); }
                catch (Exception ex) { Console.WriteLine($"[SignalR] Error in Reconnected handler: {ex.Message}"); }
            };


            await _connection.StartAsync();

            StartHeartbeat();
            Console.WriteLine("[SignalR] ✅ Connected successfully!");
            State.IsConnected = true;
            _reconnectAttempts = 0;
            _failedPings = 0;

            try
            {
                var onlineUsers = await _connection.InvokeAsync<List<Guid>>("GetOnlineUsers", CancellationToken.None);
                State.OnlineUsers = onlineUsers ?? new List<Guid>();
                foreach (var userId in State.OnlineUsers) { try { UserOnline?.Invoke(userId); } catch { } }
                Console.WriteLine($"[SignalR] Online users loaded via GetOnlineUsers: {State.OnlineUsers.Count}");
            }
            catch (Exception ex) { Console.WriteLine($"[SignalR] Failed to get online users: {ex.Message}"); }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalR] ❌ Connection failed: {ex.Message}");
            State.IsConnected = false;
            if (_connection != null) { try { await _connection.DisposeAsync(); } catch { } _connection = null; }
            throw;
        }
        finally { _connectionLock.Release(); }
    }

    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new Timer(async _ => await SendHeartbeat(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
    }

    private async Task SendHeartbeat()
    {
        if (_isDisposed || _connection == null) return;
        if (_connection.State != HubConnectionState.Connected)
        {
            if (_connection.State == HubConnectionState.Disconnected && !_isDisposed)
                _ = Task.Run(async () => { try { await ConnectAsync(); } catch (Exception ex) { Console.WriteLine($"[Heartbeat] Reconnect failed: {ex.Message}"); } });
            return;
        }
        try
        {
            await _connection.InvokeAsync("Heartbeat");
            _lastPong = DateTime.UtcNow;
            _failedPings = 0;
            if (!_heartbeatActive) { _heartbeatActive = true; Console.WriteLine("[Heartbeat] Started"); }
        }
        catch (Exception ex)
        {
            _failedPings++;
            Console.WriteLine($"[Heartbeat] Failed ({_failedPings}/3): {ex.Message}");
        }
    }

    public async Task<bool> CheckUserOnlineStatus(Guid userId)
    {
        try
        {
            if (_connection?.State != HubConnectionState.Connected)
                return false;
            var result = await _connection.InvokeAsync<object>("GetUserOnlineStatus", userId);
            var isOnline = (bool)result.GetType().GetProperty("IsOnline")?.GetValue(result, null)!;
            var isBlocked = (bool)result.GetType().GetProperty("IsBlocked")?.GetValue(result, null)!;
            return isOnline && !isBlocked;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CheckUserOnlineStatus] Error: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Guid>> GetOnlineUsersAsync()
    {
        try
        {
            if (_isDisposed || _connection?.State != HubConnectionState.Connected)
                return new List<Guid>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await _connection.InvokeAsync<List<Guid>>("GetOnlineUsers", cts.Token);

            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId == null)
                return result ?? new List<Guid>();

            var filteredResult = new List<Guid>();
            foreach (var uid in result ?? new List<Guid>())
            {
                if (!_flags.GetBlockedByMe(uid) && !_flags.GetBlockedMe(uid))
                    filteredResult.Add(uid);
            }

            Console.WriteLine($"[GetOnlineUsersAsync] Got {result?.Count ?? 0} online users, filtered to {filteredResult.Count}");
            State.OnlineUsers = filteredResult;
            return filteredResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetOnlineUsersAsync] Error: {ex.Message}");
            return new List<Guid>();
        }
    }

    public async Task<object> GetUserOnlineStatus(Guid userId)
    {
        try
        {
            // ✅ التحقق من الاتصال أولاً
            if (!await EnsureConnectionReadyAsync())
            {
                Console.WriteLine("[GetUserOnlineStatus] Connection not ready");
                return new { IsOnline = false, LastSeen = (DateTime?)null, IsBlocked = false };
            }

            try
            {
                var result = await _connection.InvokeAsync<object>("GetUserOnlineStatus", userId);

                if (result == null)
                    return new { IsOnline = false, LastSeen = (DateTime?)null, IsBlocked = false };

                var isBlocked = (bool)result.GetType().GetProperty("IsBlocked")?.GetValue(result, null)!;

                if (isBlocked)
                {
                    _flags.SetBlockedByMe(userId, true);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetUserOnlineStatus] Invoke failed: {ex.Message}");
                return new { IsOnline = false, LastSeen = (DateTime?)null, IsBlocked = false };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetUserOnlineStatus] Error: {ex.Message}");
            return new { IsOnline = false, LastSeen = (DateTime?)null, IsBlocked = false };
        }
    }
    public async Task JoinRoomAsync(Guid roomId)
    {
        _currentRoomId = roomId;

        if (_connection?.State != HubConnectionState.Connected)
            await ConnectAsync();

        try
        {
            await _connection!.InvokeAsync("JoinRoom", roomId.ToString());
            Console.WriteLine($"[SignalR] Joined room {roomId}");

            // ✅ NEW: Refresh snapshot after join
            try
            {
                var onlineUsers = await _connection.InvokeAsync<List<Guid>>("GetOnlineUsers", CancellationToken.None);

                var filtered = (onlineUsers ?? new List<Guid>())
                    .Where(uid => !_flags.GetBlockedByMe(uid) && !_flags.GetBlockedMe(uid))
                    .ToList();

                State.OnlineUsers = filtered;

                // ✅ optional: ابعت snapshot event عشان الـ VM يبني presence فورًا
                InitialOnlineUsersReceived?.Invoke(filtered);

                Console.WriteLine($"[SignalR] Online users snapshot after JoinRoom: {filtered.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] GetOnlineUsers after JoinRoom failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalR] Failed to join room {roomId}: {ex.Message}");
        }
    }

    public async Task GroupRenamedAsync(Guid roomId, string newName)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("GroupRenamed", roomId, newName);
    }

    public async Task DisconnectAsync(bool force = false)
    {
        if (!force) return;
        await _connectionLock.WaitAsync();
        try
        {
            if (_connection == null) return;
            try { await _connection.StopAsync(); } finally { await _connection.DisposeAsync(); _connection = null; State.IsConnected = false; State.OnlineUsers = Array.Empty<Guid>(); _currentRoomId = null; _reconnectAttempts = 0; }
        }
        finally { _connectionLock.Release(); }
    }

    public async Task EnsureConnectedAsync()
    {
        if (_connection?.State == HubConnectionState.Connected) return;
        if (_connection?.State == HubConnectionState.Disconnected) await ConnectAsync();
        else
        {
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(10))
            {
                if (_connection?.State == HubConnectionState.Connected) return;
                await Task.Delay(100);
            }
            throw new TimeoutException("Failed to connect to SignalR");
        }
    }

    public Task LeaveRoomAsync(Guid roomId) => _connection!.InvokeAsync("LeaveRoom", roomId.ToString());
    public Task MarkReadAsync(Guid messageId) => _connection!.InvokeAsync("MarkRead", messageId);

    public async Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId)
    {
        try
        {
            if (_connection?.State == HubConnectionState.Connected)
                await _connection.InvokeAsync("MarkRoomRead", roomId, lastMessageId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalR] MarkRoomRead failed: {ex.Message}");
            throw;
        }
    }

    public async Task NotifyTypingAsync(Guid roomId)
    {
        if (_connection is null) return;
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
        // ✅ HANDLER: MessageReceived
        _connection!.On<MessageDto>("MessageReceived", dto =>
        {
            Console.WriteLine($"[SignalR] 🟢 MESSAGE RECEIVED! ID: {dto.Id}");
            var st = (Client.Models.MessageStatus)dto.Status;
            var message = new MessageModel
            {
                Id = dto.Id,
                RoomId = dto.RoomId,
                SenderId = dto.SenderId,
                Content = dto.Content,
                CreatedAt = dto.CreatedAt,
                Status = st,
                PersonalStatus = st,
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

        // ✅ HANDLER: UserOnline - مع فلترة صارمة
        // في ChatRealtimeClient.cs - داخل RegisterHandlers()
        // ✅ HANDLER: UserOnline - مع التحقق المباشر من الـ Server
        _connection.On<Guid>("UserOnline", async (userId) =>
        {
            try
            {
                var currentUserId = await GetCurrentUserIdAsync();
                if (currentUserId == null)
                {
                    Console.WriteLine($"[SignalR] UserOnline: Cannot verify, current user is null.");
                    return;
                }

                // التحقق المحلي من الفلاغ
                var (blockedByMe, blockedMe) = _flags.GetBlockStatus(userId);

                if (blockedByMe || blockedMe)
                {
                    Console.WriteLine($"[SignalR] 🚫 UserOnline BLOCKED (local check): User={userId}");
                    return;
                }

                // ✅ التحقق من الاتصال قبل محاولة الاستدعاء
                if (!await EnsureConnectionReadyAsync())
                {
                    Console.WriteLine($"[SignalR] UserOnline: Connection not ready for server check");

                    // إذا كان الاتصال غير جاهز، نعتمد على التحقق المحلي فقط
                    var set = State.OnlineUsers.ToHashSet();
                    if (!set.Contains(userId))
                    {
                        set.Add(userId);
                        State.OnlineUsers = set.ToList();
                    }
                    UserOnline?.Invoke(userId);
                    return;
                }

                // ✅ التحقق من الـ Server بأمان
                try
                {
                    var serverStatus = await _connection.InvokeAsync<object>("GetUserOnlineStatus", userId);
                    if (serverStatus != null)
                    {
                        var isBlockedOnServer = (bool)serverStatus.GetType().GetProperty("IsBlocked")?.GetValue(serverStatus, null)!;

                        if (isBlockedOnServer)
                        {
                            Console.WriteLine($"[SignalR] 🚫 UserOnline BLOCKED (server check): User={userId}");
                            _flags.SetBlockedByMe(userId, true);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR] Server check failed (non-critical): {ex.Message}");
                    // نكمل عادي ونعتمد على التحقق المحلي
                }

                var now = DateTime.UtcNow;
                if (now - _lastUserOnlineEvent < _eventThrottle)
                    return;
                _lastUserOnlineEvent = now;

                Console.WriteLine($"[SignalR] 🔵 UserOnline ALLOWED: {userId}");
                var currentSet = State.OnlineUsers.ToHashSet();
                if (!currentSet.Contains(userId))
                {
                    currentSet.Add(userId);
                    State.OnlineUsers = currentSet.ToList();
                }
                UserOnline?.Invoke(userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] Error in UserOnline handler: {ex.Message}");
            }
        });
        _connection.On<Guid>("UserOffline", (userId) =>
        {
            try
            {
                Console.WriteLine($"[SignalR] 🔴 UserOffline: {userId}");
                var set = State.OnlineUsers.ToHashSet();
                if (set.Contains(userId))
                {
                    set.Remove(userId);
                    State.OnlineUsers = set.ToList();
                }
                UserOffline?.Invoke(userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] Error in UserOffline handler: {ex.Message}");
            }
        });

        // ✅ HANDLER: InitialOnlineUsers
        _connection.On<List<Guid>>("InitialOnlineUsers", async (onlineUsers) =>
        {
            try
            {
                Console.WriteLine($"[SignalR] 📋 Received initial online users (raw): {onlineUsers?.Count ?? 0}");

                var currentUserId = await GetCurrentUserIdAsync();
                if (currentUserId == null)
                {
                    Console.WriteLine($"[SignalR] InitialOnlineUsers: Cannot filter, current user is null.");
                    State.OnlineUsers = onlineUsers ?? new();
                    InitialOnlineUsersReceived?.Invoke(onlineUsers ?? new());
                    return;
                }

                var filteredUsers = new List<Guid>();
                foreach (var uid in onlineUsers ?? new List<Guid>())
                {
                    if (!IsUserBlocked(uid))
                        filteredUsers.Add(uid);
                    else
                        Console.WriteLine($"[SignalR] InitialOnlineUsers: Filtered out blocked user {uid}");
                }

                Console.WriteLine($"[SignalR] 📋 Initial online users (filtered): {filteredUsers.Count}");
                State.OnlineUsers = filteredUsers;
                InitialOnlineUsersReceived?.Invoke(filteredUsers);

                foreach (var userId in filteredUsers)
                {
                    try { UserOnline?.Invoke(userId); } catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] Error in InitialOnlineUsers: {ex.Message}");
            }
        });

        // ✅ HANDLER: CheckUserOnline - الحل الجذري للمشكلة
        // ✅ HANDLER: CheckUserOnline - مع حماية كاملة
        _connection.On<Guid>("CheckUserOnline", async (userId) =>
        {
            try
            {
                Console.WriteLine($"[SignalR] CheckUserOnline requested for user {userId}");

                var currentUserId = await GetCurrentUserIdAsync();
                if (currentUserId == null)
                {
                    Console.WriteLine($"[SignalR] CheckUserOnline: Cannot process, current user is null.");
                    return;
                }

                // التحقق من البلوك
                if (IsUserBlocked(userId))
                {
                    Console.WriteLine($"[SignalR] CheckUserOnline: User {userId} is BLOCKED. Ensuring offline state.");
                    var set = State.OnlineUsers.ToHashSet();
                    if (set.Contains(userId))
                    {
                        set.Remove(userId);
                        State.OnlineUsers = set.ToList();
                        UserOffline?.Invoke(userId);
                    }
                    return;
                }

                // ✅ التحقق من الاتصال قبل الاستدعاء
                if (!await EnsureConnectionReadyAsync())
                {
                    Console.WriteLine($"[SignalR] CheckUserOnline: Connection not ready");
                    return;
                }

                try
                {
                    var result = await _connection.InvokeAsync<object>("GetUserOnlineStatus", userId);

                    if (result == null)
                    {
                        Console.WriteLine($"[SignalR] CheckUserOnline: Got null result for user {userId}");
                        return;
                    }

                    var isOnline = (bool)result.GetType().GetProperty("IsOnline")?.GetValue(result, null)!;
                    var lastSeen = (DateTime?)result.GetType().GetProperty("LastSeen")?.GetValue(result, null);

                    var currentSet = State.OnlineUsers.ToHashSet();

                    if (isOnline)
                    {
                        if (!currentSet.Contains(userId))
                        {
                            currentSet.Add(userId);
                            State.OnlineUsers = currentSet.ToList();
                            UserOnline?.Invoke(userId);
                            Console.WriteLine($"[SignalR] CheckUserOnline: User {userId} is ONLINE");
                        }
                    }
                    else
                    {
                        if (currentSet.Contains(userId))
                        {
                            currentSet.Remove(userId);
                            State.OnlineUsers = currentSet.ToList();
                            UserOffline?.Invoke(userId);
                            Console.WriteLine($"[SignalR] CheckUserOnline: User {userId} is OFFLINE");
                        }
                        if (lastSeen.HasValue)
                            UserLastSeenUpdated?.Invoke(userId, lastSeen.Value);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CheckUserOnline] Invoke failed (non-critical): {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckUserOnline] Failed: {ex.Message}");
            }
        });        // باقي الـ Handlers كما هي...
        _connection.On<Guid, bool>("RoomMuteChanged", (rid, muted) =>
        {
            _flags.SetMuted(rid, muted);
            RoomMuteChanged?.Invoke(rid, muted);
        });

        _connection.On<Guid, Guid>("TypingStarted", (roomId, userId) => TypingStarted?.Invoke(roomId, userId));
        _connection.On<Guid, Guid>("TypingStopped", (roomId, userId) => TypingStopped?.Invoke(roomId, userId));
        _connection.On<Guid>("RemovedFromRoom", roomId => RemovedFromRoom?.Invoke(roomId));
        _connection.On<RoomUpdatedModel>("RoomUpdated", upd => RoomUpdated?.Invoke(upd));
        _connection.On<RoomListItemDto>("RoomUpserted", dto => RoomUpserted?.Invoke(dto));
        _connection.On<Guid, int>("RoomPresenceUpdated", (roomId, count) => RoomPresenceUpdated?.Invoke(roomId, count));
        _connection.On<Guid, bool>("UserBlockedByMeChanged", (uid, blocked) =>
        {
            Console.WriteLine($"[SignalR] 🔒 UserBlockedByMeChanged received: {uid}, blocked={blocked}");
            // تحديث الفلاغ فوراً
            _flags.SetBlockedByMe(uid, blocked);
            // إذا كان بلوك جديد، نزيل المستخدم من قائمة المتصلين فوراً
            if (blocked)
            {
                var set = State.OnlineUsers.ToHashSet();
                if (set.Contains(uid))
                {
                    set.Remove(uid);
                    State.OnlineUsers = set.ToList();
                    UserOffline?.Invoke(uid);
                    Console.WriteLine($"[SignalR] 🔴 Removed blocked user {uid} from online list immediately");
                }
            }
            UserBlockedByMeChanged?.Invoke(uid, blocked);
        });

        _connection.On<Guid, bool>("UserBlockedMeChanged", (uid, blocked) =>
        {
            Console.WriteLine($"[SignalR] 🔒 UserBlockedMeChanged received: {uid}, blocked={blocked}");
            _flags.SetBlockedMe(uid, blocked);

            // NEW: Add symmetric logic for when someone blocks me
            if (blocked)
            {
                var set = State.OnlineUsers.ToHashSet();
                if (set.Contains(uid))
                {
                    set.Remove(uid);
                    State.OnlineUsers = set.ToList();
                    UserOffline?.Invoke(uid);
                    Console.WriteLine($"[SignalR] 🔴 Removed user who blocked me {uid} from online list immediately");
                }

                // **NEW: Force refresh of online users to sync after flag update**
                // This re-calls GetOnlineUsersAsync with the new flags, ensuring filtered to 1
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await GetOnlineUsersAsync();
                        Console.WriteLine($"[UserBlockedMeChanged] Forced online users refresh after block");
                        // Optional: Re-invoke UserOffline to trigger VM/UI update
                        UserOffline?.Invoke(uid);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UserBlockedMeChanged] Refresh failed: {ex.Message}");
                    }
                });

                // Remove the GetUserOnlineStatus call to avoid null ref error
                // If needed later, fix server-side first
            }

            UserBlockedMeChanged?.Invoke(uid, blocked);
        });
        _connection.On<Guid, DateTime>("UserLastSeenUpdated", (id, lastSeen) =>
        {
            Console.WriteLine($"[SignalR] ⏱️ UserLastSeenUpdated event received for {id}: {lastSeen}");
            UserLastSeenUpdated?.Invoke(id, lastSeen);
        });
        _connection.On<Guid, Guid, int, int, int>("MessageReceiptStatsUpdated",
            (messageId, roomId, total, delivered, read) =>
            {
                MessageReceiptStatsUpdated?.Invoke(messageId, roomId, total, delivered, read);
            });
        _connection.On<Guid, Guid, int>("MessageStatusUpdated", (messageId, userId, statusInt) =>
        {
            MessageStatusUpdated?.Invoke(messageId, userId, statusInt);
        });
        _connection.On<Guid, Guid, Guid>("MessageDeliveredToAll", (messageId, senderId, roomId) =>
        {
            MessageDeliveredToAll?.Invoke(messageId, senderId, roomId);
        });
        _connection.On<Guid, Guid, Guid>("MessageReadToAll", (messageId, senderId, roomId) =>
        {
            MessageReadToAll?.Invoke(messageId, senderId, roomId);
        });
        _connection.On<Guid, Guid, int, bool>("MessageReactionUpdated",
            (messageId, userId, reactionType, isNewReaction) =>
            {
                MessageReactionUpdated?.Invoke(messageId, userId, reactionType, isNewReaction);
            });
        _connection.On<Guid, string>("GroupRenamed", (roomId, newName) => GroupRenamed?.Invoke(roomId, newName));
        _connection.On<Guid, Guid, string>("MemberAdded", (roomId, userId, displayName) => MemberAdded?.Invoke(roomId, userId, displayName));
        _connection.On<Guid, Guid, string?>("MemberRemoved", (roomId, userId, removerName) => MemberRemoved?.Invoke(roomId, userId, removerName));
        _connection.On<Guid>("GroupDeleted", roomId => GroupDeleted?.Invoke(roomId));
        _connection.On<Guid, Guid>("AdminPromoted", (roomId, userId) => AdminPromoted?.Invoke(roomId, userId));
        _connection.On<Guid, Guid>("AdminDemoted", (roomId, userId) => AdminDemoted?.Invoke(roomId, userId));
        _connection.On<Guid, Guid>("OwnerTransferred", (roomId, newOwnerId) => OwnerTransferred?.Invoke(roomId, newOwnerId));
        _connection.On<Guid, string>("MessageUpdated", (messageId, newContent) => MessageUpdated?.Invoke(messageId, newContent));
        _connection.On<Guid>("MessageDeleted", messageId => MessageDeleted?.Invoke(messageId));
        _connection.On<Guid, Guid?>("MessagePinned", (rid, mid) => MessagePinned?.Invoke(rid, mid));
        _connection.On<Guid, Guid>("MessageDelivered", (messageId, roomId) => MessageDelivered?.Invoke(messageId, roomId));
        _connection.On<Guid, Guid>("MessageRead", (messageId, roomId) => MessageRead?.Invoke(messageId, roomId));
        _connection.On<DateTime>("Pong", serverTime =>
        {
            _lastPong = DateTime.UtcNow;
            Console.WriteLine($"[Heartbeat] Pong received, server time: {serverTime}");
        });
        _connection.On<DateTime>("HeartbeatAck", serverTime =>
        {
            _lastPong = DateTime.UtcNow;
            _failedPings = 0;
            if (!State.IsConnected)
            {
                State.IsConnected = true;
                Reconnected?.Invoke();
            }
        });
    }
    private async Task<bool> EnsureConnectionReadyAsync()
    {
        try
        {
            if (_connection == null)
            {
                Console.WriteLine("[EnsureConnectionReady] Connection is null, attempting to connect...");
                await ConnectAsync();
                return _connection?.State == HubConnectionState.Connected;
            }

            if (_connection.State == HubConnectionState.Connected)
                return true;

            if (_connection.State == HubConnectionState.Disconnected)
            {
                Console.WriteLine("[EnsureConnectionReady] Connection is disconnected, attempting to reconnect...");
                await ConnectAsync();
                return _connection?.State == HubConnectionState.Connected;
            }

            Console.WriteLine($"[EnsureConnectionReady] Connection state: {_connection.State}, waiting...");

            // انتظر حتى يكتمل الاتصال
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(100);
                if (_connection?.State == HubConnectionState.Connected)
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnsureConnectionReady] Error: {ex.Message}");
            return false;
        }
    }
    public async Task SendMessageWithReplyAsync(Guid roomId, MessageModel message)
    {
        try
        {
            if (_connection == null) return;
            var request = new { RoomId = roomId, Content = message.Content, ReplyToMessageId = message.ReplyToMessageId, ReplyInfo = message.ReplyInfo };
            await _connection.InvokeAsync("SendMessageWithReply", request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SendMessageWithReply error: {ex.Message}");
        }
    }

    public Task PinMessageAsync(Guid roomId, Guid? messageId) => _connection!.InvokeAsync("PinMessage", roomId, messageId);

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _connectionLock.Dispose();
        _heartbeatTimer?.Dispose();
        _typingCts?.Dispose();
        Console.WriteLine("[SignalR] Disposed");
    }
}