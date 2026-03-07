using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace EnterpriseChat.API.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IPresenceService _presence;
    private readonly IMediator _mediator;
    private readonly IChatRoomRepository _roomRepository;
    private readonly IRoomPresenceService _roomPresence;
    private readonly ITypingService _typing;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserBlockRepository _blockRepository;      private static readonly ConcurrentDictionary<string, DateTime> _typingBroadcastGate = new();
    private static readonly ConcurrentDictionary<string, byte> _joinedRooms = new();
    private readonly IServiceScopeFactory _scopeFactory;


    public ChatHub(
        IPresenceService presence,
        IMediator mediator,
        IChatRoomRepository roomRepository,
        IRoomPresenceService roomPresence,
        ITypingService typing,
        IUserBlockRepository blockRepository,
        IMessageRepository messageRepository,
        IServiceScopeFactory scopeFactory)
    {
        _presence = presence;
        _mediator = mediator;
        _roomRepository = roomRepository;
        _roomPresence = roomPresence;
        _typing = typing;
        _blockRepository = blockRepository;
        _messageRepository = messageRepository;
        _scopeFactory = scopeFactory;
    }

        public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        Console.WriteLine($"[ChatHub] 🔵 OnConnectedAsync START for user {userId.Value}");

        await _presence.UserConnectedAsync(userId, connectionId);

                try
        {
            var visibleUsers = await GetVisibleOnlineUsersForMe(userId);
            foreach (var target in visibleUsers)
            {
                if (target == userId) continue;
                await Clients.User(target.Value.ToString()).SendAsync("UserOnline", userId.Value);
            }

            var allOnlineForMe = await GetVisibleOnlineUsersForMe(userId);
            var onlineIds = allOnlineForMe.Select(u => u.Value).ToList();
            await Clients.Caller.SendAsync("InitialOnlineUsers", onlineIds);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatHub] Error broadcasting online status: {ex.Message}");
        }

                try
        {
            var rooms = await _roomRepository.GetForUserAsync(userId, CancellationToken.None);
            foreach (var room in rooms)
            {
                await Groups.AddToGroupAsync(connectionId, room.Id.ToString());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatHub] Error joining rooms: {ex.Message}");
        }

                _ = Task.Run(async () =>
        {
            try
            {
                Console.WriteLine($"[Auto-Delivery] 🔍 Creating scope for user {userId.Value}");

                                using var scope = _scopeFactory.CreateScope();

                                var roomRepository = scope.ServiceProvider.GetRequiredService<IChatRoomRepository>();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await Task.Delay(2000);
                Console.WriteLine($"[Auto-Delivery] Delay completed for user {userId.Value}");

                var rooms = await roomRepository.GetForUserAsync(userId, CancellationToken.None);
                Console.WriteLine($"[Auto-Delivery] User {userId.Value} has {rooms.Count} rooms");

                foreach (var room in rooms)
                {
                    Console.WriteLine($"[Auto-Delivery] Checking room {room.Id.Value}");

                    var undeliveredMessages = await messageRepository.GetUndeliveredForUserAsync(room.Id, userId, CancellationToken.None);

                    Console.WriteLine($"[Auto-Delivery] Room {room.Id.Value}: found {undeliveredMessages.Count} undelivered messages");

                    foreach (var msg in undeliveredMessages)
                    {
                        Console.WriteLine($"[Auto-Delivery] Sending DeliverMessageCommand for msg {msg.Id.Value}");
                        await mediator.Send(new DeliverMessageCommand(msg.Id, userId));
                        Console.WriteLine($"[Auto-Delivery] ✅ Command sent for msg {msg.Id.Value}");
                    }
                }

                Console.WriteLine($"[Auto-Delivery] ✅ Completed for user {userId.Value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auto-Delivery] ❌ Error: {ex.Message}");
                Console.WriteLine($"[Auto-Delivery] StackTrace: {ex.StackTrace}");
            }
        });

        await base.OnConnectedAsync();
    }
    public async Task HandleBlockUpdate(Guid blockerId, Guid blockedId, bool isBlocked)
    {
        if (isBlocked)
        {
                        await Clients.User(blockerId.ToString()).SendAsync("UserOffline", blockedId);
            await Clients.User(blockedId.ToString()).SendAsync("UserOffline", blockerId);

                        var lastSeen = await _presence.GetLastSeenAsync(new UserId(blockedId));
            if (lastSeen.HasValue)
            {
                await Clients.User(blockerId.ToString()).SendAsync("UserLastSeenUpdated", blockedId, lastSeen.Value);
            }
        }
        else
        {
                        await Clients.User(blockerId.ToString()).SendAsync("CheckUserOnline", blockedId);
            await Clients.User(blockedId.ToString()).SendAsync("CheckUserOnline", blockerId);
        }
    }

    public async Task<object> GetUserOnlineStatus(Guid userId)
    {
        try
        {
                        var meRaw = Context.User?.FindFirst("sub")?.Value
                     ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? Context.User?.FindFirst("nameid")?.Value;

            if (string.IsNullOrWhiteSpace(meRaw) || !Guid.TryParse(meRaw, out var meGuid) || meGuid == Guid.Empty)
            {
                Console.WriteLine($"[GetUserOnlineStatus] No valid user in context for target {userId}. Returning default offline.");
                return new
                {
                    IsOnline = false,
                    LastSeen = (DateTime?)null,
                    IsBlocked = false
                };
            }

            var me = new UserId(meGuid);
            var target = new UserId(userId);

            bool isBlocked = false;
            try
            {
                if (_blockRepository != null)
                {
                    isBlocked = await _blockRepository.IsBlockedAsync(me, target) ||
                                await _blockRepository.IsBlockedAsync(target, me);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetUserOnlineStatus] Block check failed safely: {ex.Message}");
            }

            if (isBlocked)
            {
                return new
                {
                    IsOnline = false,
                    LastSeen = (DateTime?)null,
                    IsBlocked = true
                };
            }

            bool isOnline = await _presence.IsOnlineAsync(target);
            DateTime? lastSeen = null;

            if (!isOnline)
            {
                lastSeen = await _presence.GetLastSeenAsync(target);
            }

            return new
            {
                IsOnline = isOnline,
                LastSeen = lastSeen,
                IsBlocked = false
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetUserOnlineStatus] Safe fallback - error: {ex.Message}");
            return new
            {
                IsOnline = false,
                LastSeen = (DateTime?)null,
                IsBlocked = false
            };
        }
    }
    public async Task PinMessage(Guid roomId, Guid? messageId)
    {
        var userId = GetUserId();
        var room = await _roomRepository.GetByIdAsync(new RoomId(roomId));

        if (room == null || !room.IsMember(userId))
            throw new HubException("Not authorized");

       
        Console.WriteLine($"[Hub] PinMessage called directly - should use HTTP API instead");
    }
    public async Task Heartbeat()
    {
        try
        {
            var userId = GetUserId();

                        await _presence.UpdateHeartbeatAsync(userId);

                        await Clients.Caller.SendAsync("HeartbeatAck", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Heartbeat] Error: {ex.Message}");
                        await Clients.Caller.SendAsync("HeartbeatAck", DateTime.UtcNow);
        }
    }
    public async Task Ping()
    {
        try
        {
            var userId = GetUserId();

                        try
            {
                                if (_presence is RedisPresenceService redisPresence)
                {
                    await redisPresence.UpdateHeartbeatAsync(userId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ping] Heartbeat update failed: {ex.Message}");
            }

                        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ping] Error: {ex.Message}");
                        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
        }
    }
    public async Task AdminPromoted(Guid roomId, Guid userId)
    {
        await Clients.Group(roomId.ToString()).SendAsync("AdminPromoted", roomId, userId);
    }

    public async Task AdminDemoted(Guid roomId, Guid userId)
    {
        await Clients.Group(roomId.ToString()).SendAsync("AdminDemoted", roomId, userId);
    }
        public async Task CheckUserConnection(Guid userId)
    {
        var targetId = new UserId(userId);
        var isOnline = await _presence.IsOnlineAsync(targetId);

        if (!isOnline)
        {
            var lastSeen = await _presence.GetLastSeenAsync(targetId);
            await Clients.Caller.SendAsync("UserOffline", userId);
            if (lastSeen.HasValue)
            {
                await Clients.Caller.SendAsync("UserLastSeenUpdated", userId, lastSeen.Value);
            }
        }
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetUserId();
            var connectionId = Context.ConnectionId;

            Console.WriteLine($"[ChatHub] User {userId} disconnected, connection: {connectionId}");

                        var remainingConnections = await _presence.GetUserConnectionsCountAsync(userId);

            await _presence.UserDisconnectedAsync(userId, connectionId);

                        if (remainingConnections > 1)
            {
                Console.WriteLine($"[ChatHub] User {userId} still has {remainingConnections - 1} connections, not sending offline");
                return;
            }

                        var visibleUsers = await GetVisibleOnlineUsersForMe(userId);
            var lastSeen = DateTime.UtcNow;

            foreach (var target in visibleUsers)
            {
                if (target == userId) continue;
                await Clients.User(target.Value.ToString()).SendAsync("UserOffline", userId.Value);
                await Clients.User(target.Value.ToString()).SendAsync("UserLastSeenUpdated", userId.Value, lastSeen);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatHub] OnDisconnectedAsync error: {ex.Message}");
        }

        await base.OnDisconnectedAsync(exception);
    }
        public async Task MemberRemoved(Guid roomId, Guid userId, string removerName)
    {
        await Clients.Group(roomId.ToString())
            .SendAsync("MemberRemoved", roomId, userId, removerName);
    }
    public async Task JoinRoom(string roomIdStr)
    {
        try
        {
            var userId = GetUserId();
            var roomId = new RoomId(Guid.Parse(roomIdStr));

            var room = await _roomRepository.GetByIdAsync(roomId);
            if (room == null) throw new HubException("Room not found");

                        await Groups.AddToGroupAsync(Context.ConnectionId, roomIdStr);

                        var isFirstJoin = await _roomPresence.IsUserInRoomAsync(roomId, userId);
            if (!isFirstJoin)
            {
                await _roomPresence.JoinRoomAsync(roomId, userId);
            }

                        var count = await _roomPresence.GetOnlineCountAsync(roomId);
            await Clients.OthersInGroup(roomIdStr).SendAsync("RoomPresenceUpdated", roomId.Value, count);

                        var typingUsers = await _typing.GetTypingUsersAsync(roomId);
            if (typingUsers.Any())
            {
                var typingUserIds = typingUsers.Select(u => u.Value).ToList();
                await Clients.Caller.SendAsync("InitialTypingUsers", roomId.Value, typingUserIds);
                Console.WriteLine($"[JoinRoom] 📋 Sent {typingUsers.Count} typing users to new joiner in room {roomId.Value}");
            }

                        await Clients.Caller.SendAsync("UserOnline", userId.Value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[JoinRoom] Error: {ex.Message}");
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        var userId = GetUserId();
        var rid = new RoomId(Guid.Parse(roomId));

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        _joinedRooms.TryRemove($"{Context.ConnectionId}:{roomId}", out _);

        await _roomPresence.LeaveRoomAsync(rid, userId);

        var count = await _roomPresence.GetOnlineCountAsync(rid);
        await Clients.OthersInGroup(roomId).SendAsync("RoomPresenceUpdated", rid.Value, count);
    }

    public async Task TypingStart(string roomIdStr)
    {
        try
        {
            if (!Guid.TryParse(roomIdStr, out var roomGuid))
                return;

            var userId = GetUserId();
            var roomId = new RoomId(roomGuid);

                        var room = await _roomRepository.GetByIdAsync(roomId);
            if (room is null) return;

                        var isMember = room.IsMember(userId);
            if (!isMember) return;

                        if (room.Type == RoomType.Private)
            {
                var otherUserId = room.GetMemberIds().FirstOrDefault(id => id != userId);
                if (otherUserId != null)
                {
                    var isBlocked = await _blockRepository.IsBlockedAsync(userId, otherUserId) ||
                                   await _blockRepository.IsBlockedAsync(otherUserId, userId);

                                        if (isBlocked)
                    {
                        Console.WriteLine($"[Typing] 🚫 Blocked private room {roomId.Value}, not sending typing event");
                        return;
                    }
                }
            }

                        var gateKey = $"{roomIdStr}:{userId.Value}";
            var now = DateTime.UtcNow;

            if (_typingBroadcastGate.TryGetValue(gateKey, out var last) &&
                (now - last).TotalMilliseconds < 1000)
            {
                                await _typing.StartTypingAsync(roomId, userId, TimeSpan.FromSeconds(4));
                return;
            }

                        var isFirst = await _typing.StartTypingAsync(roomId, userId, TimeSpan.FromSeconds(4));

                        _typingBroadcastGate[gateKey] = now;

                        await Clients.OthersInGroup(roomIdStr).SendAsync("TypingStarted", roomId.Value, userId.Value);
                        if (room.Type == RoomType.Private)
            {
                var otherUserId = room.GetMemberIds().FirstOrDefault(id => id != userId);
                if (otherUserId != null)
                {
                    await Clients.User(otherUserId.Value.ToString())
                        .SendAsync("TypingStarted", roomId.Value, userId.Value);
                }
            }
            else if (room.Type == RoomType.Group)
            {
                                foreach (var memberId in room.GetMemberIds())
                {
                    if (memberId == userId) continue;
                    await Clients.User(memberId.Value.ToString())
                        .SendAsync("TypingStarted", roomId.Value, userId.Value);
                }
            }
            Console.WriteLine($"[Typing] ✍️ User {userId.Value} typing in room {roomId.Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TypingStart] Error: {ex.Message}");
        }
    }

        public async Task TypingStop(string roomIdStr)
    {
        try
        {
            if (!Guid.TryParse(roomIdStr, out var roomGuid))
                return;

            var userId = GetUserId();
            var roomId = new RoomId(roomGuid);

            await _typing.StopTypingAsync(roomId, userId);

                       
            await Clients.OthersInGroup(roomIdStr).SendAsync("TypingStopped", roomId.Value, userId.Value);
                        var roomForStop = await _roomRepository.GetByIdAsync(roomId);
            if (roomForStop?.Type == RoomType.Private)
            {
                var otherUserId = roomForStop.GetMemberIds().FirstOrDefault(id => id != userId);
                if (otherUserId != null)
                {
                    await Clients.User(otherUserId.Value.ToString())
                        .SendAsync("TypingStopped", roomId.Value, userId.Value);
                }
            }
            else if (roomForStop?.Type == RoomType.Group)
            {
                                foreach (var memberId in roomForStop.GetMemberIds())
                {
                    if (memberId == userId) continue;
                    await Clients.User(memberId.Value.ToString())
                        .SendAsync("TypingStopped", roomId.Value, userId.Value);
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TypingStop] Error: {ex.Message}");
        }
    }

        public async Task<IReadOnlyList<Guid>> GetTypingUsers(string roomIdStr)
    {
        try
        {
            if (!Guid.TryParse(roomIdStr, out var roomGuid))
                return Array.Empty<Guid>();

            var roomId = new RoomId(roomGuid);
            var typingUsers = await _typing.GetTypingUsersAsync(roomId);

            return typingUsers.Select(u => u.Value).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetTypingUsers] Error: {ex.Message}");
            return Array.Empty<Guid>();
        }
    }

    public async Task MarkRead(Guid messageId)
    {
        await _mediator.Send(new ReadMessageCommand(
            MessageId.From(messageId),
            GetUserId()));
    }

    public async Task MarkRoomRead(Guid roomId, Guid lastMessageId)
    {
                using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new MarkRoomReadCommand(
            new RoomId(roomId),
            GetUserId(),
            MessageId.From(lastMessageId)));
    }

    public async Task<IReadOnlyCollection<Guid>> GetOnlineUsers()
    {
        try
        {
            var me = GetUserId();
            var allOnline = await _presence.GetOnlineUsersAsync();

            Console.WriteLine($"[ChatHub] GetOnlineUsers - Total online from presence: {allOnline.Count}");

                        var visible = new List<Guid>();

            foreach (var u in allOnline)
            {
                try
                {
                                        if (u == me)
                    {
                        visible.Add(u.Value);
                        continue;
                    }

                                        var blocked = await _blockRepository.IsBlockedAsync(me, u);
                    if (!blocked)
                    {
                        visible.Add(u.Value);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChatHub] Error checking block for user {u}: {ex.Message}");
                }
            }

                        if (visible.Count == 0 && allOnline.Contains(me))
            {
                visible.Add(me.Value);
                Console.WriteLine($"[ChatHub] Added current user {me.Value} to online list (fallback)");
            }

            Console.WriteLine($"[ChatHub] GetOnlineUsers - Returning {visible.Count} users");
            return visible;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatHub] GetOnlineUsers error: {ex.Message}");
                        try
            {
                var me = GetUserId();
                var isOnline = await _presence.IsOnlineAsync(me);
                if (isOnline)
                {
                    return new List<Guid> { me.Value };
                }
            }
            catch { }

            return new List<Guid>();
        }
    }

    public async Task RemoveMember(Guid roomId, Guid userId)
    {
        await Clients.User(userId.ToString())
            .SendAsync("RemovedFromRoom", roomId);
    }

    public async Task GroupRenamed(Guid roomId, string newName)
    {
        await Clients.Group(roomId.ToString()).SendAsync("GroupRenamed", roomId, newName);
    }

    public async Task MemberAdded(Guid roomId, Guid userId, string displayName)
    {
        await Clients.Group(roomId.ToString()).SendAsync("MemberAdded", roomId, userId, displayName);
    }

    public async Task SendMessageWithReply(SendMessageWithReplyRequest request)
    {
        var userId = GetUserId();

        var command = new SendMessageCommand(
            new RoomId(request.RoomId),
            userId,
            request.Content,
            request.ReplyToMessageId.HasValue
                ? new MessageId(request.ReplyToMessageId.Value)
                : null);

        await _mediator.Send(command);

            }

        public async Task JoinNewRoom(string roomIdStr)
    {
        try
        {
            if (!Guid.TryParse(roomIdStr, out var roomGuid))
                return;

            var userId = GetUserId();
            var roomId = new RoomId(roomGuid);

                        var room = await _roomRepository.GetByIdWithMembersAsync(roomId, CancellationToken.None);
            if (room == null) return;

            bool isMember = room.Members.Any(m => m.UserId == userId);
            if (!isMember)
            {
                Console.WriteLine($"[JoinNewRoom] ❌ User {userId.Value} is not a member of room {roomGuid}");
                return;
            }

                        await Groups.AddToGroupAsync(Context.ConnectionId, roomIdStr);
            Console.WriteLine($"[JoinNewRoom] ✅ User {userId.Value} joined SignalR group for room {roomGuid}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[JoinNewRoom] Error: {ex.Message}");
        }
    }
    private UserId GetUserId()
{
    var raw =
        Context.User?.FindFirst("sub")?.Value
        ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? Context.User?.FindFirst("nameid")?.Value;

    if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var id) || id == Guid.Empty)
        throw new HubException("User not authenticated");

    return new UserId(id);
}
    private async Task<List<UserId>> GetVisibleOnlineUsersForMe(UserId me)
    {
        var allOnline = await _presence.GetOnlineUsersAsync();

                try
        {
            var result = new List<UserId>();
            foreach (var u in allOnline)
            {
                if (u == me) continue;

                                var blocked = await _blockRepository.IsBlockedAsync(me, u, Context.ConnectionAborted);
                if (!blocked) result.Add(u);
            }
            return result;
        }
        catch (TaskCanceledException)
        {
                        return allOnline.Where(u => u != me).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatHub] Error in GetVisibleOnlineUsersForMe: {ex.Message}");
            return allOnline.Where(u => u != me).ToList();
        }
    }

    public async Task MemberRoleChanged(Guid roomId, Guid userId, bool isAdmin)
    {
        await Clients.Group(roomId.ToString()).SendAsync("MemberRoleChanged", roomId, userId, isAdmin);
    }

    public async Task DeliverMessage(Guid messageId)
    {
        var userId = GetUserId();
        await _mediator.Send(new DeliverMessageCommand(
            MessageId.From(messageId),
            userId));
    }
}
