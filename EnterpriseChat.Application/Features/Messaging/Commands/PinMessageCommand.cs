using MediatR;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record PinMessageCommand(
    RoomId RoomId,
    MessageId? MessageId,
    TimeSpan? Duration = null) : IRequest;