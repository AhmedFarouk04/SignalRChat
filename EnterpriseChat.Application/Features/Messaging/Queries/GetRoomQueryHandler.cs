using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using MediatR;

public sealed class GetRoomQueryHandler
    : IRequestHandler<GetRoomQuery, RoomDetailsDto>
{
    private readonly IChatRoomRepository _repo;
    private readonly IRoomAuthorizationService _auth;

    public GetRoomQueryHandler(IChatRoomRepository repo, IRoomAuthorizationService auth)
    {
        _repo = repo;
        _auth = auth;
    }

    public async Task<RoomDetailsDto> Handle(GetRoomQuery request, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(request.RoomId, request.UserId, ct);

        var room = await _repo.GetByIdAsync(request.RoomId, ct);
        if (room is null)
            throw new KeyNotFoundException("Room not found");

        return new RoomDetailsDto(
            room.Id.Value,
            room.Name,
            room.Type.ToString());
    }

}
