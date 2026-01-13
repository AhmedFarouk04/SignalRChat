using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EnterpriseChat.API.Messaging;

public sealed class SignalRMessageBroadcaster : IMessageBroadcaster
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly IMutedRoomRepository _muteRepo;

    public SignalRMessageBroadcaster(
        IHubContext<ChatHub> hub,
        IMutedRoomRepository muteRepo)
    {
        _hub = hub;
        _muteRepo = muteRepo;
    }

    public async Task BroadcastMessageAsync(
        MessageDto message,
        IEnumerable<UserId> recipients)
    {
        var roomId = new RoomId(message.RoomId);

        foreach (var userId in recipients)
        {
            if (await _muteRepo.IsMutedAsync(roomId, userId))
                continue;

            await _hub.Clients.User(userId.Value.ToString())
                .SendAsync("MessageReceived", message);
        }
    }

    public async Task MessageDeliveredAsync(
        MessageId messageId,
        UserId userId)
    {
        await _hub.Clients.User(userId.Value.ToString())
            .SendAsync("MessageDelivered", messageId.Value);
    }

    public async Task MessageReadAsync(
        MessageId messageId,
        UserId userId)
    {
        await _hub.Clients.User(userId.Value.ToString())
            .SendAsync("MessageRead", messageId.Value);
    }
}
