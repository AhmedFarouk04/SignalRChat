using MediatR;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record PinMessageCommand(
    RoomId RoomId,
    MessageId? MessageId,
    UserId PinnedBy,
    TimeSpan? Duration = null,
    Guid? UnpinMessageId = null) : IRequest;