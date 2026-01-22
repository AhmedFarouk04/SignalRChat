using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed record GetMessageReadersQuery(
    MessageId MessageId
) : IRequest<IReadOnlyList<MessageReadReceiptDto>>;
