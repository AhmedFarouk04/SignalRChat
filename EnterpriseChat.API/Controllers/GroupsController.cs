using EnterpriseChat.API.Contracts.Messaging;
using EnterpriseChat.Application.DTOs;
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


    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateGroup(
    [FromBody] CreateGroupRequest request,
    CancellationToken ct)
    {
        if (request is null)
            return BadRequest("Request body is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Group name is required.");

        if (request.Name.Length > 100)
            return BadRequest("Group name is too long.");

        if (request.Members == null || request.Members.Count == 0)
            return BadRequest("At least one member is required.");

        var creatorId = GetCurrentUserId();

        var members = request.Members
            .Where(x => x != Guid.Empty && x != creatorId.Value) // exclude creator if duplicated
            .Distinct()
            .Select(x => new UserId(x))
            .ToList();

        // ✅ جديد: ولّد RoomId على server دايمًا

        var command = new CreateGroupChatCommand(
            request.Name.Trim(),
            creatorId,
            members);


        var room = await _mediator.Send(command, ct);
     



        return Created($"/api/rooms/{room.Id.Value}", new
        {
            RoomId = room.Id.Value,
            Name = room.Name,
            Type = "Group",
            OwnerId = room.OwnerId?.Value,
            MembersCount = room.Members.Count,
            CreatedAt = room.CreatedAt
        });
    }


    [HttpPost("{roomId}/members/{userId}")]
    public async Task<IActionResult> AddMember(Guid roomId, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new AddMemberToGroupCommand(new RoomId(roomId), new UserId(userId), GetCurrentUserId()), ct);

       
        return NoContent();
    }

    [HttpDelete("{roomId}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid roomId, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveMemberFromGroupCommand(new RoomId(roomId), new UserId(userId), GetCurrentUserId()), ct);

       
        return NoContent();
    }
    [HttpDelete("{roomId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid roomId, CancellationToken ct)
    {
        var requesterId = GetCurrentUserId();

        await _mediator.Send(new LeaveGroupCommand(new RoomId(roomId), requesterId), ct);

       
        return NoContent();
    }


    [HttpDelete("{roomId:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid roomId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteGroupCommand(new RoomId(roomId), GetCurrentUserId()), ct);


        return NoContent();
    }

    // GET api/groups/{roomId}/admins
    [HttpGet("{roomId}/admins")]
    public async Task<IActionResult> GetAdmins(Guid roomId, CancellationToken ct)
    {
        var admins = await _mediator.Send(
            new GetGroupAdminsQuery(new RoomId(roomId), GetCurrentUserId()),
            ct);

        return Ok(new { admins });
    }

    // POST api/groups/{roomId}/admins/{userId}
    [HttpPost("{roomId}/admins/{userId}")]
    public async Task<IActionResult> PromoteAdmin(Guid roomId, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new PromoteGroupAdminCommand(new RoomId(roomId), new UserId(userId), GetCurrentUserId()), ct);


        return NoContent();
    }
    // DELETE api/groups/{roomId}/admins/{userId}
    [HttpDelete("{roomId}/admins/{userId}")]
    public async Task<IActionResult> DemoteAdmin(Guid roomId, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new DemoteGroupAdminCommand(new RoomId(roomId), new UserId(userId), GetCurrentUserId()), ct);


        return NoContent();
    }
    // POST api/groups/{roomId}/owner/{userId}
    [HttpPost("{roomId:guid}/owner/{userId:guid}")]
    public async Task<IActionResult> TransferOwnership(Guid roomId, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new TransferGroupOwnershipCommand(new RoomId(roomId), GetCurrentUserId(), new UserId(userId)), ct);


        return NoContent();
    }
    [HttpPut("{roomId:guid}")]
    public async Task<IActionResult> Rename(Guid roomId, [FromBody] RenameGroupRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RenameGroupCommand(new RoomId(roomId), GetCurrentUserId(), request.Name), ct);


        return NoContent();
    }
    [HttpGet("{roomId:guid}")]
    public async Task<IActionResult> GetGroup(Guid roomId, CancellationToken ct)
    {
        if (roomId == Guid.Empty) return BadRequest("RoomId is required.");

        var dto = await _mediator.Send(
            new GetGroupDetailsQuery(new RoomId(roomId), GetCurrentUserId()),
            ct);

        return Ok(dto);
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

}
