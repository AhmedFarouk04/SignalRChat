using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record DeleteMessageCommand(
    MessageId MessageId,
    UserId UserId,
    bool DeleteForEveryone) : IRequest<Unit>;