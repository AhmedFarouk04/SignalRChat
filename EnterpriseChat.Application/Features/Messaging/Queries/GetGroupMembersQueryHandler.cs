using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class GetGroupMembersQueryHandler
    : IRequestHandler<GetGroupMembersQuery, GroupMembersDto>
{
    private readonly IChatRoomRepository _roomRepository;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUserLookupService _users;

    public GetGroupMembersQueryHandler(
        IChatRoomRepository roomRepository,
        IRoomAuthorizationService auth,
        IUserLookupService users)
    {
        _roomRepository = roomRepository;
        _auth = auth;
        _users = users;
    }

    public async Task<GroupMembersDto> Handle(GetGroupMembersQuery request, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(request.RoomId, request.RequesterId, ct);

        var room = await _roomRepository.GetByIdWithMembersAsync(request.RoomId, ct);
        if (room is null)
            throw new InvalidOperationException("Room not found");

        if (room.OwnerId is null)
            throw new InvalidOperationException("Group room owner not set.");

        var activeMembers = room.Members.Where(m => !m.IsRemovedFromGroup).ToList();

        var members = new List<GroupMemberDto>(activeMembers.Count);

        foreach (var m in activeMembers)
        {
            var id = m.UserId.Value;

            var displayName = await _users.GetDisplayNameAsync(id, ct)
                ?? $"User {id.ToString("N")[..6]}";

            var isAdmin = m.IsAdmin;

            members.Add(new GroupMemberDto(id, displayName, isAdmin));
        }

        return new GroupMembersDto(room.OwnerId.Value, members);
    }
}