using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed record GetMessagesQuery(
    RoomId RoomId,
    UserId UserId,
    int Skip,
    int Take
) : IRequest<IReadOnlyList<MessageReadDto>>;
