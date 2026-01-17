using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

public sealed class GetOrCreatePrivateRoomCommandHandler
    : IRequestHandler<GetOrCreatePrivateRoomCommand, PrivateRoomDto>
{
    private readonly IChatRoomRepository _repo;
    private readonly IUnitOfWork _uow;

    public GetOrCreatePrivateRoomCommandHandler(
        IChatRoomRepository repo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<PrivateRoomDto> Handle(
        GetOrCreatePrivateRoomCommand request,
        CancellationToken ct)
    {
        var room = await _repo
            .FindPrivateRoomAsync(request.UserA, request.UserB, ct);

        if (room is null)
        {
            room = ChatRoom.CreatePrivate(request.UserA, request.UserB);
            await _repo.AddAsync(room, ct);
            await _uow.CommitAsync(ct);
        }

        return new PrivateRoomDto(
            room.Id.Value,
            room.Type.ToString());
    }
}
