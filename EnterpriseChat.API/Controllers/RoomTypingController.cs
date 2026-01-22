using EnterpriseChat.API.Contracts.Messaging;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseChat.API.Controllers;

[Authorize]
[ApiController]
[Route("api/rooms")]
public sealed class RoomTypingController : BaseController
{
    private readonly ITypingService _typing;

    public RoomTypingController(ITypingService typing)
    {
        _typing = typing;
    }

    [HttpPost("{roomId:guid}/typing/start")]
    public async Task<IActionResult> StartTyping(Guid roomId, [FromBody] StartTypingRequest request, CancellationToken ct)
    {
        if (roomId == Guid.Empty) return BadRequest("RoomId is required.");
        if (request is null) return BadRequest("Request body is required.");

        var ttl = TimeSpan.FromSeconds(Math.Clamp(request.TtlSeconds, 1, 30));
        var started = await _typing.StartTypingAsync(new RoomId(roomId), GetCurrentUserId(), ttl);

        return Ok(new { started });
    }

    [HttpPost("{roomId:guid}/typing/stop")]
    public async Task<IActionResult> StopTyping(Guid roomId, CancellationToken ct)
    {
        if (roomId == Guid.Empty) return BadRequest("RoomId is required.");

        await _typing.StopTypingAsync(new RoomId(roomId), GetCurrentUserId());
        return NoContent();
    }
}
