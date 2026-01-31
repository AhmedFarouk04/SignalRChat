using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class GetGroupDetailsQueryHandler
    : IRequestHandler<GetGroupDetailsQuery, GroupDetailsDto>
{
    private readonly IChatRoomRepository _rooms;
    private readonly IUserLookupService _users;

    public GetGroupDetailsQueryHandler(IChatRoomRepository rooms, IUserLookupService users)
    {
        _rooms = rooms;
        _users = users;
    }

    public async Task<GroupDetailsDto> Handle(GetGroupDetailsQuery request, CancellationToken ct)
    {
        var room = await _rooms.GetByIdWithMembersAsync(request.RoomId, ct)
            ?? throw new KeyNotFoundException("Room not found.");

        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Not a group room.");

        if (!room.IsMember(request.RequesterId))
            throw new UnauthorizedAccessException("Access denied.");

        var members = new List<GroupMemberDetailsDto>(room.Members.Count);

        foreach (var m in room.Members)
        {
            var id = m.UserId.Value;
            var displayName = await _users.GetDisplayNameAsync(id, ct)
                ?? $"User {id.ToString()[..8]}";

            members.Add(new GroupMemberDetailsDto(
                UserId: id,
                DisplayName: displayName,
                IsOwner: m.IsOwner,
                IsAdmin: m.IsAdmin || m.IsOwner,
                JoinedAt: m.JoinedAt
            ));
        }

        return new GroupDetailsDto(
            RoomId: room.Id.Value,
            Name: room.Name,
            OwnerId: room.OwnerId?.Value,
            CreatedAt: room.CreatedAt,
            Members: members.AsReadOnly()
        );
    }
}
