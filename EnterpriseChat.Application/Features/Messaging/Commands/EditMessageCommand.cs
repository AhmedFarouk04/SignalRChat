using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands
{
    public sealed record EditMessageCommand(Guid MessageId, UserId UserId, string NewContent) : IRequest;
}
