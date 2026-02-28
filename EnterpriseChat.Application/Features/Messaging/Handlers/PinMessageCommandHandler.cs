using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

public sealed class PinMessageCommandHandler : IRequestHandler<PinMessageCommand>
{
    private readonly IChatRoomRepository _roomRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBroadcaster _broadcaster;

    public PinMessageCommandHandler(IChatRoomRepository roomRepo, IUnitOfWork unitOfWork, IMessageBroadcaster broadcaster)
    {
        _roomRepo = roomRepo;
        _unitOfWork = unitOfWork;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(PinMessageCommand request, CancellationToken ct)
    {
        var room = await _roomRepo.GetByIdWithPinsAsync(request.RoomId, ct)
                   ?? throw new Exception("Room not found");

        if (request.MessageId == null)
        {
            if (request.UnpinMessageId.HasValue)
                room.UnpinMessage(request.UnpinMessageId.Value);
            else
                room.UnpinAll();
        }
        else
        {
            room.PinMessage(request.MessageId.Value, request.Duration, request.PinnedBy);
        }

        await _unitOfWork.CommitAsync(ct);

        // ✅ الفرق: Pin → ابعت الـ ID | Unpin (أي نوع) → ابعت null دايماً
        var broadcastId = request.MessageId != null ? request.MessageId.Value : (Guid?)null; 
        await _broadcaster.NotifyMessagePinned(request.RoomId.Value, broadcastId);

        return Unit.Value;
    }
}