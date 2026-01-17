using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class GetMyRoomsQueryHandler
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
        
        var myRooms = await _rooms.GetForUserAsync(query.UserId, ct);

        var result = new List<RoomListItemDto>(myRooms.Count);

        foreach (var room in myRooms)
        {
         
            var messages = await _messages.GetByRoomAsync(room.Id, 0, 200, ct);

            var unread = 0;

            foreach (var msg in messages)
            {
                if (msg.SenderId == query.UserId)
                    continue;

                var receipt = await _receipts.GetAsync(msg.Id, query.UserId, ct);

                if (receipt is null || receipt.Status < Domain.Enums.MessageStatus.Read)
                    unread++;
            }

            string name = room.Name ?? "Chat";
            Guid? otherId = null;
            string? otherName = null;

            if (room.Type == Domain.Enums.RoomType.Private)
            {
                var otherMember = room.Members.FirstOrDefault(m => m.UserId != query.UserId);
                if (otherMember != null)
                {
                    otherId = otherMember.UserId.Value;
                    otherName = $"User {otherMember.UserId.Value.ToString()[..6]}";
                    name = otherName;
                }
            }

            result.Add(new RoomListItemDto
            {
                Id = room.Id.Value,
                Name = name,
                Type = room.Type.ToString(),
                OtherUserId = otherId,
                OtherDisplayName = otherName,
                UnreadCount = unread
            });
        }

        return result;
    }
}
