using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record ReactToMessageCommand(
    MessageId MessageId,
    UserId UserId,
    ReactionType ReactionType
) : IRequest<MessageReactionsDto>;