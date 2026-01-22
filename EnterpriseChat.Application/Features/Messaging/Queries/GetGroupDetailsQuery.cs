using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed record GetGroupDetailsQuery(RoomId RoomId, UserId RequesterId)
    : IRequest<GroupDetailsDto>;
