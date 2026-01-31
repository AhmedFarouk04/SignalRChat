using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class MarkRoomReadCommandHandler
    : IRequestHandler<MarkRoomReadCommand, Unit>
{
    private readonly IMessageRepository _messageRepo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster? _broadcaster;

    public MarkRoomReadCommandHandler(
        IMessageRepository messageRepo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster? broadcaster = null)
    {
        _messageRepo = messageRepo;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(MarkRoomReadCommand command, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.UserId, ct);

        var lastCreatedAt = await _messageRepo.GetCreatedAtAsync(command.LastMessageId, ct);
        if (lastCreatedAt is null) return Unit.Value;

        // عملنا bulk mark read في الـ DB
        await _messageRepo.BulkMarkReadUpToAsync(command.RoomId, lastCreatedAt.Value, command.UserId, ct);

        await _uow.CommitAsync(ct);

        // ✅ جديد: نبعت "MessageRead" لكل sender اللي رسائله اتقرت
        // افترض إن عندك method في IMessageRepository يرجع الرسائل ≤ lastCreatedAt (مع Id + SenderId فقط عشان performance)
        // مثال: GetMessageIdsAndSendersUpToAsync(RoomId, DateTime, ct)
        // لو مفيش، أضفه في الـ Repository (query بسيط على Messages where RoomId == ... && CreatedAt <= ...)

        var affectedMessages = await _messageRepo.GetMessageIdsAndSendersUpToAsync(command.RoomId, lastCreatedAt.Value, ct);

        if (_broadcaster is not null && affectedMessages.Any())
        {
            var tasks = affectedMessages
                .Where(m => m.SenderId != command.UserId.Value) // مش نبعت لنفسنا
                .Select(m => _broadcaster.MessageReadAsync(m.MessageId, m.SenderId));

            await Task.WhenAll(tasks);
        }

        return Unit.Value;
    }
}