using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Application.Interfaces;
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
            .ToHashSet(); // غالباً Guid HashSet

        // ✅ ابعت RoomId list مش Guid
        var roomIds = myRooms.Select(r => r.Id.Value).ToList();
        var lastByRoom = await _messages.GetLastMessagesAsync(roomIds, ct);
        var unreadByRoom = await _messages.GetUnreadCountsAsync(roomIds, query.CurrentUserId, ct);

        var nameCache = new Dictionary<Guid, string?>();
        var result = new List<RoomListItemDto>(myRooms.Count);

        foreach (var room in myRooms)
        {
            lastByRoom.TryGetValue(room.Id.Value, out var lastMsg);
            unreadByRoom.TryGetValue(room.Id.Value, out var unreadCount);

            // last message info
            // last message info
            DateTime? lastAt = lastMsg?.CreatedAt ?? room.CreatedAt; // ✅ fallback مهم
            Guid? lastId = lastMsg?.Id.Value;

            string? preview = lastMsg?.Content;
            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 60)
                preview = preview[..60];


            // NEW fields for rooms UI (status)
            Guid? lastSenderId = lastMsg?.SenderId.Value;
            MessageStatus? lastStatus = null;

            // status meaningful only if I'm sender of last message
            if (lastMsg != null && lastMsg.SenderId.Value == query.CurrentUserId.Value)
                lastStatus = lastMsg.ComputedStatusForSender;

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

            // ✅ ONE result.Add فقط
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

                // ✅ NEW
                LastMessageSenderId = lastSenderId,
                LastMessageStatus = lastStatus
            });
        }

        return result
            .OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue)
            .ToList()
            .AsReadOnly();
    }
}
