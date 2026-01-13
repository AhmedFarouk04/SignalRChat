using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record ReadMessageCommand(
    MessageId MessageId,
    UserId UserId
);
