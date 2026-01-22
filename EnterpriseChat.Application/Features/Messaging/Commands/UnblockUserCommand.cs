using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record UnblockUserCommand
    (UserId BlockerId, UserId BlockedId) 
    : IRequest<Unit>;
