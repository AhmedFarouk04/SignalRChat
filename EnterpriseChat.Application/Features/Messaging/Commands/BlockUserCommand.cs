using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record BlockUserCommand(UserId BlockerId, UserId BlockedId) : IRequest<Unit>;
