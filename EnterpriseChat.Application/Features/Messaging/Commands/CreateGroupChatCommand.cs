using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record CreateGroupChatCommand(
    string Name,
    UserId CreatorId,
    IReadOnlyCollection<UserId> Members
) : IRequest<ChatRoom>;
