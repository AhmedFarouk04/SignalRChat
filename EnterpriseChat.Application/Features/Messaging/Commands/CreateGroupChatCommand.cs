using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record CreateGroupChatCommand(
    string Name,
    UserId CreatorId,
    IReadOnlyCollection<UserId> Members
);
