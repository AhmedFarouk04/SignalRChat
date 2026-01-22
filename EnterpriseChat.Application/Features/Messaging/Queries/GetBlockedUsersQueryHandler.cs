using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class GetBlockedUsersQueryHandler
    : IRequestHandler<GetBlockedUsersQuery, IReadOnlyList<BlockedUserDto>>
{
    private readonly IUserBlockRepository _repo;
    private readonly IUserLookupService _users;

    public GetBlockedUsersQueryHandler(IUserBlockRepository repo, IUserLookupService users)
    {
        _repo = repo;
        _users = users;
    }

    public async Task<IReadOnlyList<BlockedUserDto>> Handle(GetBlockedUsersQuery request, CancellationToken ct)
    {
        var rows = await _repo.GetBlockedByBlockerAsync(request.CurrentUserId, ct);

        var result = new List<BlockedUserDto>(rows.Count);

        foreach (var x in rows)
        {
            var id = x.BlockedId.Value;
            var displayName = await _users.GetDisplayNameAsync(id, ct)
                ?? $"User {id.ToString()[..8]}";

            result.Add(new BlockedUserDto(
                UserId: id,
                DisplayName: displayName,
                CreatedAt: x.CreatedAt
            ));
        }

        return result.AsReadOnly();
    }
}
