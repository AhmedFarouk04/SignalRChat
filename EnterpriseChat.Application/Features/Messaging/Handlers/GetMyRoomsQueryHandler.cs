using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Common;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        var myRooms = await _rooms.GetForUserAsync(query.CurrentUserId, ct);
        var mutedRoomIds = (await _mutes.GetMutedRoomIdsAsync(query.CurrentUserId, ct))
            .ToHashSet();

        var roomIds = myRooms.Select(r => r.Id.Value).ToList();

        // جلب آخر رسالة لكل روم مرة واحدة فقط (كفاءة عالية)
        var lastByRoom = await _messages.GetLastMessagesAsync(roomIds, ct);

        var unreadByRoom = await GetUnreadCountsWithLastReadAsync(myRooms, query.CurrentUserId, ct);

        var nameCache = new Dictionary<Guid, string?>();
        var result = new List<RoomListItemDto>(myRooms.Count);

        foreach (var room in myRooms)
        {
            lastByRoom.TryGetValue(room.Id.Value, out var lastMsg);

            // ✅ الرسالة المرئية فقط في RoomList
            var lastVisibleMsg = GetVisibleLastMessage(lastMsg);

            DateTime? lastAt = lastVisibleMsg?.CreatedAt ?? room.CreatedAt;
            Guid? lastId = lastVisibleMsg?.Id.Value;
            string? preview = lastVisibleMsg?.Content;
            Guid? lastSenderId = lastVisibleMsg?.SenderId.Value;

            // ✅ لو الـ reaction أحدث من آخر رسالة، واللي عمل الـ reaction مش صاحب الرسالة (ده اللي بيشوف الـ preview)
            bool reactionIsNewer = room.LastReactionAt.HasValue &&
                room.LastReactionTargetUserId?.Value == query.CurrentUserId.Value &&
                room.LastReactionAt > (lastVisibleMsg?.CreatedAt ?? DateTime.MinValue);

            if (reactionIsNewer)
            {
                preview = room.LastReactionPreview;
                lastAt = room.LastReactionAt;
                lastId = null;
                lastSenderId = null;
            }

            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 60)
                preview = preview[..60];

            // حساب الـ Status (double check)
            MessageStatus? lastStatus = null;
            if (lastVisibleMsg != null && lastVisibleMsg.SenderId.Value == query.CurrentUserId.Value)
            {
                if (room.Type == RoomType.Group)
                    lastStatus = ComputeGroupStatus(lastVisibleMsg.TotalRecipients, lastVisibleMsg.DeliveredCount, lastVisibleMsg.ReadCount);
                else
                    lastStatus = (lastVisibleMsg.ReadCount >= lastVisibleMsg.TotalRecipients) ? MessageStatus.Read
                             : (lastVisibleMsg.DeliveredCount > 0) ? MessageStatus.Delivered
                             : MessageStatus.Sent;
            }

            // Private Chat Name
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

            // LastRead من الـ Member
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

    // ✅ دالة جديدة: تتحكم إيه اللي يظهر في RoomList
    private static LastMessageInfo? GetVisibleLastMessage(LastMessageInfo? lastMsg)
    {
        if (lastMsg == null) return null;

        if (!lastMsg.IsSystemMessage)
            return lastMsg;

        // فقط الـ system الشخصي يظهر في RoomList
        return lastMsg.SystemMessageType switch
        {
            SystemMessageType.MemberAdded => lastMsg,
            SystemMessageType.MemberRemoved => lastMsg,
            _ => null   // joined, left, renamed, edited, etc → مش هتظهر أبدًا
        };
    }

    // باقي الدوال كما هي (بدون أي تغيير)
    private async Task<Dictionary<Guid, int>> GetUnreadCountsWithLastReadAsync(
        IEnumerable<ChatRoom> rooms,
        UserId userId,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var room in rooms)
        {
            var member = room.Members.FirstOrDefault(m => m.UserId == userId);
            var lastReadId = member?.LastReadMessageId;
            int unreadCount = 0;

            if (lastReadId is not null)
            {
                var lastReadMsg = await _messages.GetByIdAsync(lastReadId, ct);
                if (lastReadMsg != null)
                {
                    unreadCount = await _messages.GetUnreadCountAsync(
                        new RoomId(room.Id), lastReadMsg.CreatedAt, userId, ct);
                }
                else
                {
                    unreadCount = await _messages.GetTotalUnreadCountAsync(new RoomId(room.Id), userId, ct);
                }
            }
            else
            {
                unreadCount = await _messages.GetTotalUnreadCountAsync(new RoomId(room.Id), userId, ct);
            }

            result[room.Id.Value] = unreadCount;
        }
        return result;
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