using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Application.Interfaces;
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
        var myRooms = await _rooms.GetForUserAsync(query.CurrentUserId, ct);

        var mutedRoomIds = (await _mutes.GetMutedRoomIdsAsync(query.CurrentUserId, ct))
            .ToHashSet();

        var roomIds = myRooms.Select(r => r.Id.Value).ToList();
        var lastByRoom = await _messages.GetLastMessagesAsync(roomIds, ct);

        // ✅ استخدم الميثود الجديدة اللي بتحسب unread count باستخدام LastReadMessageId
        var unreadByRoom = await GetUnreadCountsWithLastReadAsync(myRooms, query.CurrentUserId, ct);

        var nameCache = new Dictionary<Guid, string?>();
        var result = new List<RoomListItemDto>(myRooms.Count);

        foreach (var room in myRooms)
        {
            lastByRoom.TryGetValue(room.Id.Value, out var lastMsg);
            unreadByRoom.TryGetValue(room.Id.Value, out var unreadCount);

            // last message info
            DateTime? lastAt = lastMsg?.CreatedAt ?? room.CreatedAt;
            Guid? lastId = lastMsg?.Id.Value;

            string? preview = lastMsg?.Content;
            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 60)
                preview = preview[..60];

            // NEW fields for rooms UI (status)
            Guid? lastSenderId = lastMsg?.SenderId.Value;
            MessageStatus? lastStatus = null;

            // status meaningful only if I'm sender of last message
            if (lastMsg != null && lastMsg.SenderId.Value == query.CurrentUserId.Value)
            {
                if (room.Type == RoomType.Group)
                    lastStatus = ComputeGroupStatus(lastMsg.TotalRecipients, lastMsg.DeliveredCount, lastMsg.ReadCount);
                else
                    lastStatus = (lastMsg.ReadCount >= lastMsg.TotalRecipients) ? MessageStatus.Read
                             : (lastMsg.DeliveredCount > 0) ? MessageStatus.Delivered
                             : MessageStatus.Sent;
            }


            // defaults
            string name = room.Name ?? "Chat";
            Guid? otherUserId = null;
            string? otherDisplayName = null;

            // private naming
            if (room.Type == RoomType.Private)
            {
                var otherMember = room.Members
                    .FirstOrDefault(m => m.UserId.Value != query.CurrentUserId.Value);

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

            // ✅ جلب LastReadMessageId من الـ Member
            var member = room.Members.FirstOrDefault(m => m.UserId.Value == query.CurrentUserId.Value);
            Guid? lastReadMessageId = member?.LastReadMessageId?.Value;
            DateTime? lastReadAt = member?.LastReadAt;

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

                // ✅ NEW - إرسال LastReadMessageId للـ Client
                LastReadMessageId = lastReadMessageId,
                LastReadAt = lastReadAt
            });
        }

        return result
            .OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue)
            .ToList()
            .AsReadOnly();
    }

    // ✅ ميثود مساعدة لحساب unread count باستخدام LastReadMessageId
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

            if (lastReadId is not null)  // ✅ التعديل هنا
            {
                var lastReadMsg = await _messages.GetByIdAsync(lastReadId, ct);
                if (lastReadMsg != null)
                {
                    unreadCount = await _messages.GetUnreadCountAsync(
                        new RoomId(room.Id),
                        lastReadMsg.CreatedAt,
                        userId,
                        ct);
                }
                else
                {
                    unreadCount = await _messages.GetTotalUnreadCountAsync(
                        new RoomId(room.Id),
                        userId,
                        ct);
                }
            }
            else
            {
                unreadCount = await _messages.GetTotalUnreadCountAsync(
                    new RoomId(room.Id),
                    userId,
                    ct);
            }

            result[room.Id.Value] = unreadCount;
        }

        return result;
    }
    private static MessageStatus ComputeGroupStatus(int total, int delivered, int read)
    {
        if (total <= 0) return MessageStatus.Sent;
        if (read >= total) return MessageStatus.Read;

        var half = (int)Math.Ceiling(total / 2.0);
        if (delivered >= half) return MessageStatus.Delivered;

        return MessageStatus.Sent;
    }

}