using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using MediatR;

public sealed class GetRoomQueryHandler
    : IRequestHandler<GetRoomQuery, RoomDetailsDto>
{
    private readonly IRoomAuthorizationService _auth;
    private readonly IRoomDetailsReader _reader;

    public GetRoomQueryHandler(IRoomAuthorizationService auth, IRoomDetailsReader reader)
    {
        _auth = auth;
        _reader = reader;
    }

    public async Task<RoomDetailsDto> Handle(GetRoomQuery request, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(request.RoomId, request.UserId, ct);

        var dto = await _reader.GetRoomDetailsAsync(request.RoomId.Value, request.UserId.Value, ct);
        if (dto is null) throw new KeyNotFoundException("Room not found");

        return dto;
    }
}
