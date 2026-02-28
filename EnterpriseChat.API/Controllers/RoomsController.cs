using EnterpriseChat.API.Contracts.Messaging;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
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
    private readonly IChatRoomRepository _roomRepo;

    public RoomsController(IMediator mediator, IChatRoomRepository roomRepo)
    {
        _mediator = mediator;
        _roomRepo = roomRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyRooms(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyRoomsQuery(GetCurrentUserId()), ct));

    [HttpGet("{roomId}")]
    public async Task<IActionResult> GetRoom(Guid roomId, CancellationToken ct)
    {
        if (roomId == Guid.Empty) return BadRequest("RoomId is required.");
        return Ok(await _mediator.Send(new GetRoomQuery(new RoomId(roomId), GetCurrentUserId()), ct));
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

        await _mediator.Send(new PinMessageCommand(
            new RoomId(roomId),
            dto.MessageId.HasValue ? new MessageId(dto.MessageId.Value) : null,
            new UserId(GetCurrentUserId()),  // ✅ PinnedBy
            duration,
            dto.UnpinMessageId));            // ✅ UnpinMessageId

        return Ok();
    }

    [HttpGet("{roomId}/pins")]
    public async Task<IActionResult> GetPins(Guid roomId, CancellationToken ct)
    {
        var room = await _roomRepo.GetByIdWithPinsAsync(new RoomId(roomId), ct);
        if (room == null) return NotFound();

        var pins = room.PinnedMessages
            .Where(p => !p.IsExpired())
            .OrderBy(p => p.PinnedAt)
            .Select(p => p.MessageId.Value)
            .ToList();

        return Ok(pins);
    }
}