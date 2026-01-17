using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Features.Messaging.Handlers;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EnterpriseChat.API.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IPresenceService _presence;
    private readonly DeliverMessageCommandHandler _deliverHandler;
    private readonly ReadMessageCommandHandler _readHandler;
    private readonly IChatRoomRepository _roomRepository;
    private readonly IRoomPresenceService _roomPresence;
    private readonly ITypingService _typing;
    private readonly MarkRoomReadCommandHandler _markRoomReadHandler;
    public ChatHub(
        IPresenceService presence,
        DeliverMessageCommandHandler deliverHandler,
        ReadMessageCommandHandler readHandler,
        IChatRoomRepository roomRepository,
        IRoomPresenceService roomPresence,
        ITypingService typing,
        MarkRoomReadCommandHandler markRoomReadHandler)
    {
        _presence = presence;
        _deliverHandler = deliverHandler;
        _readHandler = readHandler;
        _roomRepository = roomRepository;
        _roomPresence = roomPresence;
        _typing = typing;
        _markRoomReadHandler = markRoomReadHandler;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        var wasOnline = await _presence.IsOnlineAsync(userId);
        await _presence.UserConnectedAsync(userId, connectionId);

        if (!wasOnline)
            await Clients.Others.SendAsync("UserOnline", userId.Value);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        await _presence.UserDisconnectedAsync(userId, connectionId);

        // ✅ remove user from all opened rooms
        var affectedRooms = await _roomPresence.RemoveUserFromAllRoomsAsync(userId);

        foreach (var rid in affectedRooms)
        {
            var count = await _roomPresence.GetOnlineCountAsync(rid);
            await Clients.Group(rid.Value.ToString())
                .SendAsync("RoomPresenceUpdated", rid.Value, count);
        }

        if (!await _presence.IsOnlineAsync(userId))
            await Clients.Others.SendAsync("UserOffline", userId.Value);

        await base.OnDisconnectedAsync(exception);
    }


    public async Task JoinRoom(string roomId)
    {
        var userId = GetUserId();
        var rid = new RoomId(Guid.Parse(roomId));
        var room = await _roomRepository.GetByIdAsync(rid);

        if (room is null || !room.IsMember(userId))
            throw new HubException("Access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // Presence per room
        await _roomPresence.JoinRoomAsync(rid, userId);

        // Broadcast online count داخل الروم
        var count = await _roomPresence.GetOnlineCountAsync(rid);
        await Clients.Group(roomId).SendAsync("RoomPresenceUpdated", rid.Value, count);

        // Deliver pending
        await _deliverHandler.DeliverRoomMessagesAsync(rid, userId);
    }
    


   

    public async Task TypingStart(string roomId)
    {
        var userId = GetUserId();
        var rid = new RoomId(Guid.Parse(roomId));
        var room = await _roomRepository.GetByIdAsync(rid);

        if (room is null || !room.IsMember(userId))
            return;

        // TTL: 4 seconds
        var first = await _typing.StartTypingAsync(rid, userId, TimeSpan.FromSeconds(4));

        // ابعت event فقط لو first time (منع spam)
        if (first)
        {
            await Clients.OthersInGroup(roomId)
                .SendAsync("TypingStarted", rid.Value, userId.Value);
        }
    }

    // اختياري (مش لازم لو هتعتمد على TTL)
    public async Task TypingStop(string roomId)
    {
        var userId = GetUserId();
        var rid = new RoomId(Guid.Parse(roomId));

        await _typing.StopTypingAsync(rid, userId);

        await Clients.OthersInGroup(roomId)
            .SendAsync("TypingStopped", rid.Value, userId.Value);
    }
    public async Task LeaveRoom(string roomId)
    {
        var userId = GetUserId();
        var rid = new RoomId(Guid.Parse(roomId));

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

        await _roomPresence.LeaveRoomAsync(rid, userId);

        var count = await _roomPresence.GetOnlineCountAsync(rid);
        await Clients.Group(roomId).SendAsync("RoomPresenceUpdated", rid.Value, count);
    }



    public async Task MarkRead(Guid messageId)
    {
        await _readHandler.Handle(
            new ReadMessageCommand(
                MessageId.From(messageId),
                GetUserId()));
    }

    public async Task<IReadOnlyCollection<Guid>> GetOnlineUsers()
    {
        var users = await _presence.GetOnlineUsersAsync();
        return users.Select(u => u.Value).ToList();
    }

    private UserId GetUserId()
    {
        var id = Context.User?.FindFirst("sub")?.Value
            ?? throw new HubException("User not authenticated");

        return new UserId(Guid.Parse(id));
    }
    public async Task MarkRoomRead(
    Guid roomId,
    Guid lastMessageId)
    {
        await _markRoomReadHandler.Handle(
            new MarkRoomReadCommand(
                new RoomId(roomId),
                GetUserId(),
                MessageId.From(lastMessageId)));
    }

    public async Task RemoveMember(Guid roomId, Guid userId)
    {
        await Clients.User(userId.ToString())
            .SendAsync("RemovedFromRoom", roomId);
    }


}
