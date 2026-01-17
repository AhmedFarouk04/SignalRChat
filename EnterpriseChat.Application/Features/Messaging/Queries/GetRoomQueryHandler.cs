using EnterpriseChat.Application.DTOs;
using MediatR;

public sealed class GetRoomQueryHandler
    : IRequestHandler<GetRoomQuery, RoomDetailsDto>
{
    private readonly IChatRoomRepository _repo;

    public GetRoomQueryHandler(IChatRoomRepository repo)
    {
        _repo = repo;
    }

    public async Task<RoomDetailsDto> Handle(
        GetRoomQuery request,
        CancellationToken ct)
    {
        var room = await _repo.GetByIdAsync(request.RoomId, ct);
        if (room is null)
            throw new KeyNotFoundException("Room not found");

        return new RoomDetailsDto(
            room.Id.Value,
            room.Name,
            room.Type.ToString());
    }
}
