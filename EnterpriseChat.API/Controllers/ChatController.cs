using EnterpriseChat.API.Contracts.Messaging;
using EnterpriseChat.API.Hubs;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;


[Authorize]
[ApiController]
[Route("api/chat")]
public sealed class ChatController : BaseController
{
    private readonly IMediator _mediator;
    private readonly IHubContext<ChatHub> _hub;

    public ChatController(IMediator mediator, IHubContext<ChatHub> hub)
    {
        _mediator = mediator;
        _hub = hub;
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
            new GetMessageReadersQuery(MessageId.From(messageId), GetCurrentUserId())
,
            ct));
    }


    [HttpPost("private/{userId}")]
    public async Task<IActionResult> GetOrCreatePrivateChat(Guid userId, CancellationToken ct)
    {
        var dto = await _mediator.Send(
            new GetOrCreatePrivateRoomCommand(GetCurrentUserId(), new UserId(userId)),
            ct);

        return Ok(dto); // ✅ { id, type }
    }


    [HttpPost("block/{userId}")]
    public async Task<IActionResult> BlockUser(Guid userId, CancellationToken ct)
    {
        var me = GetCurrentUserId();

        await _mediator.Send(new BlockUserCommand(me, new UserId(userId)), ct);

        await _hub.Clients.User(me.Value.ToString())
            .SendAsync("UserBlockChanged", userId, true, ct);

        return NoContent();
    }


    [HttpPost("mute/{roomId}")]
    public async Task<IActionResult> MuteRoom(Guid roomId, CancellationToken ct)
    {
        var me = GetCurrentUserId();

        await _mediator.Send(new MuteRoomCommand(new RoomId(roomId), me), ct);

        await _hub.Clients.User(me.Value.ToString())
            .SendAsync("RoomMuteChanged", roomId, true, ct);

        return NoContent();
    }

    [HttpDelete("mute/{roomId}")]
    public async Task<IActionResult> UnmuteRoom(Guid roomId, CancellationToken ct)
    {
        var me = GetCurrentUserId();

        await _mediator.Send(new UnmuteRoomCommand(new RoomId(roomId), me), ct);

        await _hub.Clients.User(me.Value.ToString())
            .SendAsync("RoomMuteChanged", roomId, false, ct);

        return NoContent();
    }



    // POST /api/chat/messages/{messageId}/delivered
    [HttpPost("messages/{messageId:guid}/delivered")]
    public async Task<IActionResult> MarkMessageDelivered(Guid messageId, CancellationToken ct)
    {
        if (messageId == Guid.Empty)
            return BadRequest("MessageId is required.");

        await _mediator.Send(
            new DeliverMessageCommand(MessageId.From(messageId), GetCurrentUserId()),
            ct);

        return NoContent();
    }

    // POST /api/chat/messages/{messageId}/read
    [HttpPost("messages/{messageId:guid}/read")]
    public async Task<IActionResult> MarkMessageRead(Guid messageId, CancellationToken ct)
    {
        if (messageId == Guid.Empty)
            return BadRequest("MessageId is required.");

        await _mediator.Send(
            new ReadMessageCommand(MessageId.From(messageId), GetCurrentUserId()),
            ct);

        return NoContent();
    }

    // POST /api/chat/rooms/{roomId}/delivered
    [HttpPost("rooms/{roomId:guid}/delivered")]
    public async Task<IActionResult> DeliverRoomMessages(Guid roomId, CancellationToken ct)
    {
        if (roomId == Guid.Empty)
            return BadRequest("RoomId is required.");

        await _mediator.Send(
            new DeliverRoomMessagesCommand(new RoomId(roomId), GetCurrentUserId()),
            ct);

        return NoContent();
    }

    // POST /api/chat/rooms/{roomId}/read
    [HttpPost("rooms/{roomId:guid}/read")]
    public async Task<IActionResult> MarkRoomRead(Guid roomId, [FromBody] MarkRoomReadRequest request, CancellationToken ct)
    {
        if (roomId == Guid.Empty)
            return BadRequest("RoomId is required.");

        if (request is null)
            return BadRequest("Request body is required.");

        if (request.LastMessageId == Guid.Empty)
            return BadRequest("LastMessageId is required.");

        await _mediator.Send(
            new MarkRoomReadCommand(
                new RoomId(roomId),
                GetCurrentUserId(),
                MessageId.From(request.LastMessageId)),
            ct);

        return NoContent();
    }

    [HttpDelete("block/{userId:guid}")]
    public async Task<IActionResult> UnblockUser(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return BadRequest("UserId is required.");

        var me = GetCurrentUserId();

        await _mediator.Send(new UnblockUserCommand(me, new UserId(userId)), ct);

        await _hub.Clients.User(me.Value.ToString())
            .SendAsync("UserBlockChanged", userId, false, ct);

        return NoContent();
    }


    [HttpGet("blocked")]
    [ProducesResponseType(typeof(IReadOnlyList<BlockedUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlocked(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetBlockedUsersQuery(GetCurrentUserId()), ct));
    }

    [HttpGet("muted")]
    [ProducesResponseType(typeof(IReadOnlyList<MutedRoomDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMuted(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetMutedRoomsQuery(GetCurrentUserId()), ct));
    }



[HttpPost("rooms/{roomId:guid}/attachments")]
[Consumes("multipart/form-data")]
[RequestSizeLimit(25_000_000)]
public async Task<IActionResult> UploadAttachment(
    Guid roomId,
    [FromServices] IAttachmentService attachments,
    [FromForm] UploadAttachmentForm form,
    CancellationToken ct)
{
    if (roomId == Guid.Empty) return BadRequest("RoomId is required.");
    if (form?.File is null || form.File.Length == 0) return BadRequest("File is required.");

    await using var stream = form.File.OpenReadStream();

    var dto = await attachments.UploadAsync(
        new RoomId(roomId),
        GetCurrentUserId(),
        stream,
        form.File.FileName,
        form.File.ContentType ?? "application/octet-stream",
        form.File.Length,
        ct);

    return Ok(dto);
}



[HttpGet("rooms/{roomId:guid}/attachments")]
    public async Task<IActionResult> ListRoomAttachments(
    Guid roomId,
    [FromServices] ChatDbContext db,
    [FromServices] IRoomAuthorizationService auth,
    [FromQuery] int skip = 0,
    [FromQuery] int take = 50,
    CancellationToken ct = default)
    {
        if (roomId == Guid.Empty) return BadRequest("RoomId is required.");
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);

        await auth.EnsureUserIsMemberAsync(new RoomId(roomId), GetCurrentUserId(), ct);

        var list = await db.Attachments.AsNoTracking()
            .Where(a => a.RoomId == roomId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(a => new AttachmentDto(
                a.Id, a.RoomId, a.UploaderId, a.FileName, a.ContentType, a.Size,
                $"/api/attachments/{a.Id}", a.CreatedAt))
            .ToListAsync(ct);

        return Ok(list);
    }


}
