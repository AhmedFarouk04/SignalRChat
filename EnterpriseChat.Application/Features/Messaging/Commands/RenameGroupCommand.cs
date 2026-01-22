using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record RenameGroupCommand(
    RoomId RoomId,
    UserId RequesterId,
    string Name
) : IRequest<Unit>;
