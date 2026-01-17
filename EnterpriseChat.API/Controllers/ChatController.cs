using EnterpriseChat.API.Contracts.Messaging;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

public sealed class ChatController : BaseController
{
    private readonly IMediator _mediator;

    public ChatController(IMediator mediator)
    {
        _mediator = mediator;
    }


    [HttpPost("messages")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        if (request.RoomId == Guid.Empty)
            return BadRequest("RoomId is required.");

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Message content is required.");

        if (request.Content.Length > 2000)
            return BadRequest("Message too long.");

        var result = await _mediator.Send(
            new SendMessageCommand(
                new RoomId(request.RoomId),
                GetCurrentUserId(),
                request.Content),
            ct);

        return Ok(result);
    }

    [HttpGet("rooms/{roomId}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageReadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(
        Guid roomId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (roomId == Guid.Empty)
            return BadRequest("RoomId is required.");

        return Ok(await _mediator.Send(
            new GetMessagesQuery(
                new RoomId(roomId),
                GetCurrentUserId(),
                skip,
                take),
            ct));
    }

    [HttpGet("messages/{messageId}/readers")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageReadReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReaders(
        Guid messageId,
        CancellationToken ct)
    {
        return Ok(await _mediator.Send(
            new GetMessageReadersQuery(MessageId.From(messageId)),
            ct));
    }


    [HttpPost("private/{userId}")]
    public async Task<IActionResult> GetOrCreatePrivateChat(
        Guid userId,
        CancellationToken ct)
    {
        var room = await _mediator.Send(
            new GetOrCreatePrivateRoomCommand(
                GetCurrentUserId(),
                new UserId(userId)),
            ct);

        return Ok(new { room.Id, room.Type });
    }

    [HttpPost("block/{userId}")]
    public async Task<IActionResult> BlockUser(Guid userId, CancellationToken ct)
    {
        await _mediator.Send(
            new BlockUserCommand(
                GetCurrentUserId(),
                new UserId(userId)),
            ct);

        return NoContent();
    }

    [HttpPost("mute/{roomId}")]
    public async Task<IActionResult> MuteRoom(Guid roomId, CancellationToken ct)
    {
        await _mediator.Send(
            new MuteRoomCommand(
                new RoomId(roomId),
                GetCurrentUserId()),
            ct);

        return NoContent();
    }

    [HttpDelete("mute/{roomId}")]
    public async Task<IActionResult> UnmuteRoom(Guid roomId, CancellationToken ct)
    {
        await _mediator.Send(
            new UnmuteRoomCommand(
                new RoomId(roomId),
                GetCurrentUserId()),
            ct);

        return NoContent();
    }
}
