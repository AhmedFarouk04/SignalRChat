using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class GetGroupMembersQueryHandler
    : IRequestHandler<GetGroupMembersQuery, GroupMembersDto>
{
    private readonly IChatRoomRepository _roomRepository;

    public GetGroupMembersQueryHandler(IChatRoomRepository roomRepository)
    {
        _roomRepository = roomRepository;
    }

    public async Task<GroupMembersDto> Handle(
        GetGroupMembersQuery request,
        CancellationToken ct)
    {
        var room = await _roomRepository.GetByIdAsync(request.RoomId, ct);

        if (room is null)
            throw new InvalidOperationException("Room not found");

        if (!room.Members.Any(m => m.UserId == request.RequesterId))
            throw new UnauthorizedAccessException();

        return new GroupMembersDto(
            room.OwnerId.Value,
            room.Members.Select(m =>
                new GroupMemberDto(
                    m.UserId.Value,
                    $"User {m.UserId.Value.ToString()[..6]}"
                )).ToList());
    }
}
