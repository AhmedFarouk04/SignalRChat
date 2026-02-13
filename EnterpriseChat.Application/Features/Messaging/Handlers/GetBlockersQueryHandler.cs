using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Moderation.Queries;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Application.Features.Moderation.Handlers;

public class GetBlockersQueryHandler : IRequestHandler<GetBlockersQuery, IReadOnlyList<BlockerDto>>
{
    private readonly IUserBlockRepository _blockRepo;
    private readonly IUserDirectoryService _userDirectory;

    public GetBlockersQueryHandler(
        IUserBlockRepository blockRepo,
        IUserDirectoryService userDirectory)
    {
        _blockRepo = blockRepo;
        _userDirectory = userDirectory;
    }

    public async Task<IReadOnlyList<BlockerDto>> Handle(GetBlockersQuery request, CancellationToken ct)
    {
        var blockers = await _blockRepo.GetBlockersOfUserAsync(new Domain.ValueObjects.UserId(request.CurrentUserId), ct);

        var result = new List<BlockerDto>();
        foreach (var block in blockers)
        {
            var user = await _userDirectory.GetUserSummaryAsync(block.BlockerId, ct);
            result.Add(new BlockerDto(
                block.BlockerId.Value,
                user?.DisplayName,
                block.CreatedAt));
        }

        return result.AsReadOnly();
    }
}