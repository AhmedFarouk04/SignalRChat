using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Application.Features.Messaging.Queries;
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
    private readonly IMessageReceiptRepository _receipts;
    private readonly IMutedRoomRepository _mutes;
    private readonly IUserLookupService _users;

    public GetMyRoomsQueryHandler(
        IChatRoomRepository rooms,
        IMessageRepository messages,
        IMessageReceiptRepository receipts,
        IMutedRoomRepository mutes,
        IUserLookupService users)
    {
        _rooms = rooms;
        _messages = messages;
        _receipts = receipts;
        _mutes = mutes;
        _users = users;
    }

    public async Task<IReadOnlyList<RoomListItemDto>> Handle(GetMyRoomsQuery query, CancellationToken ct)
    {
        var myRooms = await _rooms.GetForUserAsync(query.CurrentUserId, ct);
        var result = new List<RoomListItemDto>(myRooms.Count);

        foreach (var room in myRooms)
        {
            var isMuted = await _mutes.IsMutedAsync(room.Id, query.CurrentUserId, ct);

            var lastMsgList = await _messages.GetByRoomAsync(room.Id, skip: 0, take: 1, ct);
            var lastMsg = lastMsgList.LastOrDefault();
            DateTime? lastAt = lastMsg?.CreatedAt;
            Guid? lastId = lastMsg?.Id.Value;
            string? preview = lastMsg?.Content;
            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 60)
                preview = preview[..60];

            var recentMessages = await _messages.GetByRoomAsync(room.Id, skip: 0, take: 200, ct);
            int unreadCount = 0;
            foreach (var msg in recentMessages)
            {
                if (msg.SenderId == query.CurrentUserId)
                    continue;

                var receipt = await _receipts.GetAsync(msg.Id, query.CurrentUserId, ct);
                if (receipt == null || receipt.Status < MessageStatus.Read)
                    unreadCount++;
            }

            string name = room.Name ?? "Chat";
            Guid? otherUserId = null;
            string? otherDisplayName = null;

            if (room.Type == RoomType.Private)
            {
                var otherMember = room.Members.FirstOrDefault(m => m.UserId != query.CurrentUserId);
                if (otherMember != null)
                {
                    otherUserId = otherMember.UserId.Value;
                    otherDisplayName = await _users.GetDisplayNameAsync(otherUserId.Value, ct)
                        ?? $"User {otherUserId.Value.ToString()[..8]}";
                    name = otherDisplayName;
                }
            }

            result.Add(new RoomListItemDto
            {
                Id = room.Id.Value,
                Name = name,
                Type = room.Type.ToString(),
                OtherUserId = otherUserId,
                OtherDisplayName = otherDisplayName,
                UnreadCount = unreadCount,
                IsMuted = isMuted,
                LastMessageAt = lastAt,
                LastMessageId = lastId,
                LastMessagePreview = preview
            });
        }

        return result
            .OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue)
            .ToList()
            .AsReadOnly();
    }
}
