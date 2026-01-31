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

    public async Task BroadcastMessageAsync(MessageDto message, IEnumerable<UserId> recipients)
    {
        // ✅ في private chat recipients غالبًا واحد، بس برضه نخليها bulk-safe
        var tasks = recipients.Select(r =>
            _hub.Clients.User(r.Value.ToString())
                .SendAsync("MessageReceived", message));

        await Task.WhenAll(tasks);
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

   public async Task RoomUpdatedAsync(RoomUpdatedDto update, IEnumerable<UserId> users)
{
    var roomId = new RoomId(update.RoomId);

    // ✅ لو تقدر: اعمل bulk muted مرة واحدة بدل IsMutedAsync لكل مستخدم
    // لو مش عندك: سيبها foreach زي ما كانت
    var tasks = new List<Task>();

    foreach (var userId in users)
    {
        if (await _muteRepo.IsMutedAsync(roomId, userId))
            continue;

        var delta = update.UnreadDelta < 0
            ? update.UnreadDelta
            : (userId.Value == update.SenderId ? 0 : update.UnreadDelta);

        var perUserUpdate = new RoomUpdatedDto
        {
            RoomId = update.RoomId,
            MessageId = update.MessageId,
            SenderId = update.SenderId,
            Preview = update.Preview,
            CreatedAt = update.CreatedAt,
            UnreadDelta = delta
        };

        tasks.Add(_hub.Clients.User(userId.Value.ToString())
            .SendAsync("RoomUpdated", perUserUpdate));
    }

    await Task.WhenAll(tasks);
}


}