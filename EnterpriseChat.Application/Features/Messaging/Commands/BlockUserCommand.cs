using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record BlockUserCommand(
    UserId BlockerId,
    UserId BlockedId
);
