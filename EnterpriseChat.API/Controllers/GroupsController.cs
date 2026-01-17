using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseChat.API.Controllers;

[Authorize]
[ApiController]
[Route("api/groups")]
public sealed class GroupsController : BaseController
{
    private readonly IMediator _mediator;

    public GroupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{roomId}/members")]
    public async Task<IActionResult> GetMembers(Guid roomId, CancellationToken ct)
    {
        return Ok(await _mediator.Send(
            new GetGroupMembersQuery(
                new RoomId(roomId),
                GetCurrentUserId()),
            ct));
    }

    [HttpPost("{roomId}/members/{userId}")]
    public async Task<IActionResult> AddMember(
        Guid roomId,
        Guid userId,
        CancellationToken ct)
    {
        await _mediator.Send(
            new AddMemberToGroupCommand(
                new RoomId(roomId),
                new UserId(userId),
                GetCurrentUserId()),
            ct);

        return NoContent();
    }

    [HttpDelete("{roomId}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(
        Guid roomId,
        Guid userId,
        CancellationToken ct)
    {
        await _mediator.Send(
            new RemoveMemberFromGroupCommand(
                new RoomId(roomId),
                new UserId(userId),
                GetCurrentUserId()),
            ct);

        return NoContent();
    }
}
