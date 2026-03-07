using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Common;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class GetMyRoomsQueryHandler
    : IRequestHandler<GetMyRoomsQuery, IReadOnlyList<RoomListItemDto>>
{
    private readonly IChatRoomRepository _rooms;
    private readonly IMessageRepository _messages;
    private readonly IMutedRoomRepository _mutes;
    private readonly IUserLookupService _users;

    public GetMyRoomsQueryHandler(
        IChatRoomRepository rooms,
        IMessageRepository messages,
        IMutedRoomRepository mutes,
        IUserLookupService users)
    {
        _rooms = rooms;
        _messages = messages;
        _mutes = mutes;
        _users = users;
    }

    public async Task<IReadOnlyList<RoomListItemDto>> Handle(GetMyRoomsQuery query, CancellationToken ct)
    {
        var allRooms = await _rooms.GetForUserAsync(query.CurrentUserId, ct);
        Console.WriteLine($"[BACKEND DEBUG] Total rooms found in DB for user {query.CurrentUserId}: {allRooms.Count}");
        foreach (var r in allRooms)
        {
            var me = r.Members.FirstOrDefault(m => m.UserId == query.CurrentUserId);
            Console.WriteLine($"[BACKEND DEBUG] Room: {r.Id.Value}, Type: {r.Type}, IsMember: {me != null}, IsDeleted: {me?.IsDeleted}");
        }
        var myRooms = allRooms
            .Where(r => r.Members.Any(m => m.UserId.Value == query.CurrentUserId.Value && !m.IsDeleted))
            .ToList();
        var mutedRoomIds = (await _mutes.GetMutedRoomIdsAsync(query.CurrentUserId, ct))
            .ToHashSet();

        var roomIds = myRooms.Select(r => r.Id.Value).ToList();

        var lastByRoom = await _messages.GetLastMessagesAsync(roomIds, ct);
        var unreadByRoom = await _messages.GetUnreadCountsAsync(roomIds, query.CurrentUserId, ct);

                var clearedAtByRoom = myRooms
            .ToDictionary(
                r => r.Id.Value,
                r => r.Members
                      .FirstOrDefault(m => m.UserId == query.CurrentUserId)
                      ?.ClearedAt
            );

        var nameCache = new Dictionary<Guid, string?>();
        var result = new List<RoomListItemDto>(myRooms.Count);

        foreach (var room in myRooms)
        {
            lastByRoom.TryGetValue(room.Id.Value, out var lastMsg);

                        var clearedAt = clearedAtByRoom.GetValueOrDefault(room.Id.Value);
            if (clearedAt.HasValue && lastMsg != null && lastMsg.CreatedAt <= clearedAt.Value)
            {
                lastMsg = null;
            }

            var lastVisibleMsg = GetVisibleLastMessage(lastMsg);

            DateTime? lastAt = lastVisibleMsg?.CreatedAt ?? room.CreatedAt;
            Guid? lastId = lastVisibleMsg?.Id.Value;
            string? preview = lastVisibleMsg?.Content;
            Guid? lastSenderId = lastVisibleMsg?.SenderId.Value;

            bool reactionIsNewer =
                room.LastReactionAt.HasValue &&
                room.LastReactionTargetUserId?.Value == query.CurrentUserId.Value &&
                room.LastReactionAt > (lastVisibleMsg?.CreatedAt ?? DateTime.MinValue);

                        if (reactionIsNewer && (!clearedAt.HasValue || room.LastReactionAt > clearedAt.Value))
            {
                preview = room.LastReactionPreview;
                lastAt = room.LastReactionAt;
                lastId = null;
                lastSenderId = null;
            }

            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 60)
                preview = preview[..60];

            MessageStatus? lastStatus = null;
            if (lastVisibleMsg != null && lastVisibleMsg.SenderId.Value == query.CurrentUserId.Value)
            {
                lastStatus = room.Type == RoomType.Group
                    ? ComputeGroupStatus(lastVisibleMsg.TotalRecipients, lastVisibleMsg.DeliveredCount, lastVisibleMsg.ReadCount)
                    : (lastVisibleMsg.ReadCount >= lastVisibleMsg.TotalRecipients) ? MessageStatus.Read
                    : (lastVisibleMsg.DeliveredCount > 0) ? MessageStatus.Delivered
                    : MessageStatus.Sent;
            }

            string name = room.Name ?? "Chat";
            Guid? otherUserId = null;
            string? otherDisplayName = null;

            if (room.Type == RoomType.Private)
            {
                var otherMember = room.Members.FirstOrDefault(m => m.UserId.Value != query.CurrentUserId.Value);
                if (otherMember != null)
                {
                    otherUserId = otherMember.UserId.Value;
                    if (!nameCache.TryGetValue(otherUserId.Value, out otherDisplayName))
                    {
                        otherDisplayName = await _users.GetDisplayNameAsync(otherUserId.Value, ct);
                        nameCache[otherUserId.Value] = otherDisplayName;
                    }
                    otherDisplayName ??= $"User {otherUserId.Value.ToString()[..8]}";
                    name = otherDisplayName;
                }
            }

            var member = room.Members.FirstOrDefault(m => m.UserId.Value == query.CurrentUserId.Value);
            Guid? lastReadMessageId = member?.LastReadMessageId?.Value;
            DateTime? lastReadAt = member?.LastReadAt;

            var memberNames = new Dictionary<Guid, string>();
            if (room.Type == RoomType.Group)
            {
                foreach (var m in room.Members)
                {
                    if (m.UserId.Value == query.CurrentUserId.Value) continue;
                    if (!nameCache.TryGetValue(m.UserId.Value, out var mName))
                    {
                        mName = await _users.GetDisplayNameAsync(m.UserId.Value, ct);
                        nameCache[m.UserId.Value] = mName;
                    }
                    if (!string.IsNullOrWhiteSpace(mName))
                        memberNames[m.UserId.Value] = mName;
                }
            }

            unreadByRoom.TryGetValue(room.Id.Value, out var unreadCount);

            result.Add(new RoomListItemDto
            {
                Id = room.Id.Value,
                Name = name,
                Type = room.Type.ToString(),
                OtherUserId = otherUserId,
                OtherDisplayName = otherDisplayName,
                UnreadCount = unreadCount,
                IsMuted = mutedRoomIds.Contains(room.Id.Value),
                LastMessageAt = lastAt,
                LastMessageId = lastId,
                LastMessagePreview = preview,
                LastMessageSenderId = lastSenderId,
                LastMessageStatus = lastStatus,
                LastReadMessageId = lastReadMessageId,
                LastReadAt = lastReadAt,
                MemberNames = memberNames
            });
        }

        return result
            .OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue)
            .ToList()
            .AsReadOnly();
    }

    private static LastMessageInfo? GetVisibleLastMessage(LastMessageInfo? lastMsg)
    {
        if (lastMsg == null) return null;
        if (!lastMsg.IsSystemMessage) return lastMsg;
        return lastMsg.SystemMessageType switch
        {
            SystemMessageType.MemberAdded => lastMsg,
            SystemMessageType.MemberRemoved => lastMsg,
            _ => null
        };
    }

    private static MessageStatus ComputeGroupStatus(int totalRecipients, int deliveredCount, int readCount)
    {
        if (totalRecipients <= 0) return MessageStatus.Sent;
        if (readCount >= totalRecipients) return MessageStatus.Read;
        var halfOrMore = (int)Math.Ceiling(totalRecipients / 2.0);
        if (readCount >= halfOrMore) return MessageStatus.Delivered;
        return MessageStatus.Sent;
    }
}