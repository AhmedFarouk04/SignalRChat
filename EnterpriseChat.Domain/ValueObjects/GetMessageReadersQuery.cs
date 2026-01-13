using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed record GetMessageReadersQuery(
    MessageId MessageId
);
