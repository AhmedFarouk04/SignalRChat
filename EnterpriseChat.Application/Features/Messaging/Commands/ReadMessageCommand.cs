using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record ReadMessageCommand(
    MessageId MessageId,
    UserId UserId
) : IRequest<Unit>;
