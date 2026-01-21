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

    public GetGroupMembersQueryHandler(
        IChatRoomRepository roomRepository,
        IRoomAuthorizationService auth)
    {
        _roomRepository = roomRepository;
        _auth = auth;
    }

    public async Task<GroupMembersDto> Handle(GetGroupMembersQuery request, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(request.RoomId, request.RequesterId, ct);

        var room = await _roomRepository.GetByIdWithMembersAsync(request.RoomId, ct);
        if (room is null)
            throw new InvalidOperationException("Room not found");

        if (room.OwnerId is null)
            throw new InvalidOperationException("Group room owner not set.");

        return new GroupMembersDto(
            room.OwnerId.Value,
            room.Members.Select(m =>
                new GroupMemberDto(
                    m.UserId.Value,
                    $"User {m.UserId.Value.ToString()[..6]}"
                )).ToList());
    }
}
