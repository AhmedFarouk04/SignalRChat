using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class GetMessagesQueryHandler
{
    private readonly IMessageReadRepository _repository;
    private readonly IChatRoomRepository _roomRepository;

    public GetMessagesQueryHandler(
        IMessageReadRepository repository,
        IChatRoomRepository roomRepository)
    {
        _repository = repository;
        _roomRepository = roomRepository;
    }

    public async Task<IReadOnlyList<MessageReadDto>> Handle(
        GetMessagesQuery query,
        CancellationToken cancellationToken = default)
    {
        var room = await _roomRepository.GetByIdAsync(query.RoomId, cancellationToken)
            ?? throw new InvalidOperationException("Room not found.");

        if (!room.IsMember(query.UserId))
            throw new UnauthorizedAccessException("Access denied.");

        return await _repository.GetMessagesAsync(
            query.RoomId,
            query.Skip,
            query.Take,
            cancellationToken);
    }
}
