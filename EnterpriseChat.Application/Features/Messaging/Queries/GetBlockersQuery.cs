using EnterpriseChat.Application.DTOs;
using MediatR;

namespace EnterpriseChat.Application.Features.Moderation.Queries;

public record GetBlockersQuery(Guid CurrentUserId) : IRequest<IReadOnlyList<BlockerDto>>;

