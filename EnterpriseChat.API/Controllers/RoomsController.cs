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
[Route("api/rooms")]
public sealed class RoomsController : BaseController
{
    private readonly IMediator _mediator;

    public RoomsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET api/rooms
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyRooms(CancellationToken ct)
    {
        return Ok(await _mediator.Send(
            new GetMyRoomsQuery(GetCurrentUserId()),
            ct));
    }

    // GET api/rooms/{roomId}
    [HttpGet("{roomId}")]
    public async Task<IActionResult> GetRoom(Guid roomId, CancellationToken ct)
    {
        if (roomId == Guid.Empty)
            return BadRequest("RoomId is required.");

                return Ok(await _mediator.Send(
            new GetRoomQuery(
                new RoomId(roomId),
                GetCurrentUserId()),
            ct));
    }

    [HttpPost("{roomId}/pin")]
    public async Task<IActionResult> PinMessage(Guid roomId, [FromBody] PinRequest dto)
    {
        var duration = dto.Duration switch
        {
            "24h" => TimeSpan.FromHours(24),
            "7d" => TimeSpan.FromDays(7),
            "30d" => TimeSpan.FromDays(30),
            _ => (TimeSpan?)null
        };

        await _mediator.Send(new PinMessageCommand(new RoomId(roomId),
            dto.MessageId.HasValue ? new MessageId(dto.MessageId.Value) : null,
            duration));

        return Ok();
    }

}

