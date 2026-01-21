using EnterpriseChat.Domain.ValueObjects;
using MediatR;


namespace EnterpriseChat.Application.Features.Messaging.Queries;



public sealed record GetGroupAdminsQuery(
    RoomId RoomId,
    UserId RequesterId
) : IRequest<IReadOnlyList<Guid>>;
