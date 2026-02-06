using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using EnterpriseChat.Domain.Enums;

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
        var tasks = recipients.Select(r =>
            _hub.Clients.User(r.Value.ToString())
                .SendAsync("MessageReceived", message));

        await Task.WhenAll(tasks);
    }

  

    // عدل الطرق الحالية لترسل لكل الأعضاء
    public async Task MessageDeliveredAsync(
        MessageId messageId,
        UserId userId)
    {
        // احصل على أعضاء الغرفة أولاً (سنضيف هذا المنطق في الـ Handler)
        // حالياً نرسل للمرسل فقط (للتوافق مع الكود الحالي)
        await _hub.Clients.User(userId.Value.ToString())
            .SendAsync("MessageDelivered", messageId.Value);
    }


    public async Task MemberLeftAsync(RoomId roomId, UserId memberId, IEnumerable<UserId> users)
    {
        var tasks = users.DistinctBy(u => u.Value)
            .Select(u => _hub.Clients.User(u.Value.ToString())
                .SendAsync("MemberLeft", roomId.Value, memberId.Value));

        await Task.WhenAll(tasks);
    }

    public async Task RemovedFromRoomAsync(RoomId roomId, UserId userId)
    {
        await _hub.Clients.User(userId.Value.ToString())
            .SendAsync("RemovedFromRoom", roomId.Value);
    }

    public async Task RemovedFromRoomAsync(RoomId roomId, IEnumerable<UserId> users)
    {
        var tasks = users.DistinctBy(u => u.Value)
            .Select(u => _hub.Clients.User(u.Value.ToString())
                .SendAsync("RemovedFromRoom", roomId.Value));

        await Task.WhenAll(tasks);
    }

    public async Task GroupDeletedAsync(RoomId roomId, IEnumerable<UserId> users)
    {
        var tasks = users.DistinctBy(u => u.Value)
            .Select(u => _hub.Clients.User(u.Value.ToString())
                .SendAsync("GroupDeleted", roomId.Value));

        await Task.WhenAll(tasks);
    }

    public async Task AdminPromotedAsync(RoomId roomId, UserId userId, IEnumerable<UserId> users)
    {
        var tasks = users.DistinctBy(u => u.Value)
            .Select(u => _hub.Clients.User(u.Value.ToString())
                .SendAsync("AdminPromoted", roomId.Value, userId.Value));

        await Task.WhenAll(tasks);
    }

    public async Task AdminDemotedAsync(RoomId roomId, UserId userId, IEnumerable<UserId> users)
    {
        var tasks = users.DistinctBy(u => u.Value)
            .Select(u => _hub.Clients.User(u.Value.ToString())
                .SendAsync("AdminDemoted", roomId.Value, userId.Value));

        await Task.WhenAll(tasks);
    }

    public async Task OwnerTransferredAsync(RoomId roomId, UserId newOwnerId, IEnumerable<UserId> users)
    {
        var tasks = users.DistinctBy(u => u.Value)
            .Select(u => _hub.Clients.User(u.Value.ToString())
                .SendAsync("OwnerTransferred", roomId.Value, newOwnerId.Value));

        await Task.WhenAll(tasks);
    }

    public async Task RoomUpdatedAsync(RoomUpdatedDto update, IEnumerable<UserId> users)
    {
        var roomId = new RoomId(update.RoomId);
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

    public async Task RoomUpsertedAsync(RoomListItemDto room, IEnumerable<UserId> users)
    {
        var roomId = new RoomId(room.Id);
        var tasks = new List<Task>();

        foreach (var userId in users.DistinctBy(u => u.Value))
        {
            if (await _muteRepo.IsMutedAsync(roomId, userId))
                continue;

            tasks.Add(_hub.Clients.User(userId.Value.ToString())
                .SendAsync("RoomUpserted", room));
        }

        await Task.WhenAll(tasks);
    }

    public async Task MemberAddedAsync(RoomId roomId, UserId memberId, string displayName, IEnumerable<UserId> users)
    {
        var tasks = new List<Task>();

        foreach (var userId in users.DistinctBy(u => u.Value))
        {
            if (await _muteRepo.IsMutedAsync(roomId, userId))
                continue;

            tasks.Add(_hub.Clients.User(userId.Value.ToString())
                .SendAsync("MemberAdded", roomId.Value, memberId.Value, displayName));
        }

        await Task.WhenAll(tasks);
    }

    public async Task MemberRemovedAsync(RoomId roomId, UserId memberId, UserId? removerId, string? removerName, IEnumerable<UserId> users)
    {
        var tasks = new List<Task>();

        foreach (var userId in users.DistinctBy(u => u.Value))
        {
            if (await _muteRepo.IsMutedAsync(roomId, userId))
                continue;

            tasks.Add(_hub.Clients.User(userId.Value.ToString())
                .SendAsync("MemberRemoved", roomId.Value, memberId.Value, removerName));
        }

        // ✅ أضف هذا الـ Call للمجموعة
        await _hub.Clients.Group(roomId.Value.ToString())
            .SendAsync("MemberRemoved", roomId.Value, memberId.Value, removerName);

        await Task.WhenAll(tasks);
    }

    public async Task MemberRemovedAsync(RoomId roomId, UserId memberId, IEnumerable<UserId> users)
    {
        await MemberRemovedAsync(roomId, memberId, null, null, users);
    }

   


    public async Task MessageReadAsync(
    MessageId messageId,
    UserId userId)
    {
        await _hub.Clients.User(userId.Value.ToString())
            .SendAsync("MessageRead", messageId.Value);
    }

    public async Task MessageReadToAllAsync(
    MessageId messageId,
    UserId senderId,
    IEnumerable<UserId> roomMembers)
    {
        var tasks = roomMembers.Select(memberId =>
            _hub.Clients.User(memberId.Value.ToString())
                .SendAsync("MessageReadToAll",
                    messageId.Value,
                    senderId.Value));

        await Task.WhenAll(tasks);
    }
    public async Task MessageDeliveredToAllAsync(
    MessageId messageId,
    UserId senderId,
    IEnumerable<UserId> roomMembers)
    {
        var tasks = roomMembers.Select(memberId =>
            _hub.Clients.User(memberId.Value.ToString())
                .SendAsync("MessageDeliveredToAll",
                    messageId.Value,
                    senderId.Value));

        await Task.WhenAll(tasks);
    }

    public async Task MessageStatusUpdatedAsync(
    MessageId messageId,
    UserId userId,
    MessageStatus newStatus,
    IEnumerable<UserId> roomMembers)
    {
        var tasks = roomMembers.Select(memberId =>
            _hub.Clients.User(memberId.Value.ToString())
                .SendAsync("MessageStatusUpdated",
                    messageId.Value,
                    userId.Value,
                    (int)newStatus));

        await Task.WhenAll(tasks);
    }
    // في SignalRMessageBroadcaster.cs أضف:
    public async Task MessageReactionUpdatedAsync(
        MessageId messageId,
        UserId userId,
        ReactionType reactionType,
        bool isNewReaction,
        IEnumerable<UserId> roomMembers)
    {
        var tasks = roomMembers.Select(memberId =>
            _hub.Clients.User(memberId.Value.ToString())
                .SendAsync("MessageReactionUpdated",
                    messageId.Value,
                    userId.Value,
                    (int)reactionType,
                    isNewReaction));

        await Task.WhenAll(tasks);
    }
}