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
    private readonly IUserBlockRepository _blockRepository; // إضافة مستودع الحظر

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
        var room = await _roomRepository.GetByIdWithMembersAsync(query.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");

        if (!room.IsMember(query.UserId))
            throw new UnauthorizedAccessException("Access denied.");

        var messages = await _repository.GetMessagesAsync(
            query.RoomId,
            query.UserId,  // ← لازم تكون مررته
            query.Skip,
            query.Take,
            ct);

        // ← الفلتر المهم جدًا: أظهر بس الرسائل اللي المستخدم sender فيها أو ليه receipt
        var filtered = messages.Where(m =>
            m.SenderId == query.UserId.Value ||                  // أنا اللي بعتها
            m.Receipts.Any(r => r.UserId == query.UserId.Value)  // ليا receipt (يعني مش كنت blocked وقت الإرسال)
        ).ToList();

        // لو private وأنا بلوكته حاليًا → اخفي رسائله كلها
        if (room.Type == RoomType.Private)
        {
            var otherUser = room.Members.FirstOrDefault(m => m.UserId != query.UserId)?.UserId;
            if (otherUser != null)
            {
                var isBlockedByMe = await _blockRepository.IsBlockedAsync(query.UserId, otherUser, ct);
                if (isBlockedByMe)
                {
                    filtered = filtered.Where(m => m.SenderId != otherUser.Value).ToList();
                }
            }
        }

        return filtered;
    }
}