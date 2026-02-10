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
        var room = await _roomRepo.GetByIdAsync(request.RoomId, ct)
                   ?? throw new Exception("Room not found");

        // تحديث الغرفة بالرسالة المثبتة
        // السطر رقم 25 في الـ Handler الخاص بك يجب أن يكون هكذا:
        room.PinMessage(request.MessageId?.Value, request.Duration);
        await _unitOfWork.CommitAsync(ct);

        // إرسال الإشارة عبر SignalR للجميع في الغرفة
        await _broadcaster.NotifyMessagePinned(request.RoomId.Value, request.MessageId?.Value);

        return Unit.Value;
    }
}