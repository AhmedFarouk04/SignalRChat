using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class GetMutedRoomsQueryHandler
    : IRequestHandler<GetMutedRoomsQuery, IReadOnlyList<MutedRoomDto>>
{
    private readonly IMutedRoomRepository _repo;

    public GetMutedRoomsQueryHandler(IMutedRoomRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<MutedRoomDto>> Handle(GetMutedRoomsQuery request, CancellationToken ct)
    {
        var rows = await _repo.GetMutedRoomsAsync(request.CurrentUserId, ct);

        return rows.Select(x => new MutedRoomDto(
            RoomId: x.RoomId.Value,
            MutedAt: x.MutedAt
        )).ToList().AsReadOnly();
    }
}
