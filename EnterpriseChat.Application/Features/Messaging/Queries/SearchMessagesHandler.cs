
using MediatR;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class SearchMessagesQueryHandler
    : IRequestHandler<SearchMessagesQuery, IReadOnlyList<MessageReadDto>>
{
    private readonly IMessageReadRepository _readRepository;
    private readonly IChatRoomRepository _roomRepository;

    public SearchMessagesQueryHandler(
        IMessageReadRepository readRepository,
        IChatRoomRepository roomRepository)
    {
        _readRepository = readRepository;
        _roomRepository = roomRepository;
    }

    public async Task<IReadOnlyList<MessageReadDto>> Handle(SearchMessagesQuery query, CancellationToken ct)
    {
        var roomId = new RoomId(query.RoomId);
        var userId = new UserId(query.UserId);

        var room = await _roomRepository.GetByIdWithMembersAsync(roomId, ct)
                   ?? throw new InvalidOperationException("Room not found.");

        if (!room.IsMember(userId))
            throw new UnauthorizedAccessException("Access denied.");

        return await _readRepository.SearchMessagesAsync(roomId, query.SearchTerm, query.Take, ct);
    }
}