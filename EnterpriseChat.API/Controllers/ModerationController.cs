using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Moderation.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/moderation")]
public sealed class ModerationController : BaseController
{
    private readonly IMediator _mediator;

    public ModerationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("blocked-by-me")]
    [ProducesResponseType(typeof(IReadOnlyList<BlockedUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlockedByMe(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();

        var blockers = await _mediator.Send(
            new GetBlockersQuery(currentUserId),
            ct);

        var dtos = blockers.Select(b => new BlockedUserDto(
            b.BlockerId,
            b.BlockerDisplayName ?? "User",
            b.CreatedAt
        )).ToList();

        return Ok(dtos);
    }

}