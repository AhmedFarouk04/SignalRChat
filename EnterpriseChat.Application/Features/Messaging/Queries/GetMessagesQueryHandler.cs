using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.Enums;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class GetMessagesQueryHandler
    : IRequestHandler<GetMessagesQuery, IReadOnlyList<MessageReadDto>>
{
    private readonly IMessageReadRepository _repository;
    private readonly IChatRoomRepository _roomRepository;
    private readonly IUserBlockRepository _blockRepository;

    public GetMessagesQueryHandler(
        IMessageReadRepository repository,
        IChatRoomRepository roomRepository,
        IUserBlockRepository blockRepository)
    {
        _repository = repository;
        _roomRepository = roomRepository;
        _blockRepository = blockRepository;
    }

    public async Task<IReadOnlyList<MessageReadDto>> Handle(
        GetMessagesQuery query,
        CancellationToken ct)
    {
        // ✅ Validation بياخد ct العادي — كويس إنه يتكنسل لو الـ client قطع قبل ما نبدأ
        var room = await _roomRepository.GetByIdWithMembersAsync(query.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (!room.IsMember(query.UserId))
            throw new UnauthorizedAccessException("Access denied.");

        // ✅ بنمرر ct للـ repository لكن الـ repository نفسه بيستخدم CancellationToken.None
        // في الـ AsSplitQuery queries — الـ ct بيتجاهل للـ DB operations
        // وده هو الحل الصح لـ TaskCanceledException
        var messages = await _repository.GetMessagesAsync(
            query.RoomId,
            query.UserId,
            query.Skip,
            query.Take,
            ct);    // ← repository هيستخدم None داخلياً

        if (room.Type == RoomType.Private)
        {
            var otherUser = room.Members
                .FirstOrDefault(m => m.UserId != query.UserId)?.UserId;

            if (otherUser != null)
            {
                // ✅ IsBlockedAsync سريع (< 5ms) — ct عادي كويس هنا
                var isBlockedByMe = await _blockRepository
                    .IsBlockedAsync(query.UserId, otherUser, CancellationToken.None);

                if (isBlockedByMe)
                {
                    return messages
                        .Where(m => m.SenderId == query.UserId.Value)
                        .ToList();
                }
            }
        }

        return messages;
    }
}