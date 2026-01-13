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
    public ChatHub(
        IPresenceService presence,
        DeliverMessageCommandHandler deliverHandler,
        ReadMessageCommandHandler readHandler,
        IChatRoomRepository roomRepository)
    {
        _presence = presence;
        _deliverHandler = deliverHandler;
        _readHandler = readHandler;
        _roomRepository = roomRepository;
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

        if (!await _presence.IsOnlineAsync(userId))
            await Clients.Others.SendAsync("UserOffline", userId.Value);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomId)
    {
        var userId = GetUserId();
        var room = await _roomRepository.GetByIdAsync(new RoomId(Guid.Parse(roomId)));

        if (room is null || !room.IsMember(userId))
            throw new HubException("Access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        await _deliverHandler.DeliverRoomMessagesAsync(
            new RoomId(Guid.Parse(roomId)),
            userId);
    }

    public async Task Typing(string roomId)
    {
        var userId = GetUserId();
        var room = await _roomRepository.GetByIdAsync(new RoomId(Guid.Parse(roomId)));

        if (room is null || !room.IsMember(userId))
            return;

        await Clients.OthersInGroup(roomId)
            .SendAsync("UserTyping", userId.Value);
    }


    public Task LeaveRoom(string roomId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

   

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
}
