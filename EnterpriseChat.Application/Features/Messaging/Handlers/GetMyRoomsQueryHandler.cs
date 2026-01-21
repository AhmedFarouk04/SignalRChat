using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class GetMyRoomsQueryHandler : IRequestHandler<GetMyRoomsQuery, IReadOnlyList<RoomListItemDto>>
{
    private readonly IChatRoomRepository _rooms;
    private readonly IMessageRepository _messages;
    private readonly IMessageReceiptRepository _receipts;

    public GetMyRoomsQueryHandler(
        IChatRoomRepository rooms,
        IMessageRepository messages,
        IMessageReceiptRepository receipts)
    {
        _rooms = rooms;
        _messages = messages;
        _receipts = receipts;
    }

    public async Task<IReadOnlyList<RoomListItemDto>> Handle(
        GetMyRoomsQuery query,
        CancellationToken ct = default)
    {
        var myRooms = await _rooms.GetForUserAsync(query.CurrentUserId, ct);

        var result = new List<RoomListItemDto>(myRooms.Count);

        foreach (var room in myRooms)
        {
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
                    otherDisplayName = $"User {otherUserId.Value.ToString()[..8]}";
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
                UnreadCount = unreadCount
            });
        }

        return result.AsReadOnly();
    }
}