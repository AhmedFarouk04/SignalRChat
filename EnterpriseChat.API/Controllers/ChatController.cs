using EnterpriseChat.API.Contracts.Messaging;
using EnterpriseChat.API.Extensions;
using EnterpriseChat.API.Hubs;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Application.Features.Moderation.Queries;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Application.Services;
using EnterpriseChat.Domain.Interfaces;
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
        request.Content,
        request.ReplyToMessageId.HasValue
            ? new MessageId(request.ReplyToMessageId.Value)
            : null),
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

        // ✅ تحديث فوري: إخفاء حالة الأونلاين عند الطرفين فوراً
        await _hub.Clients.User(me.Value.ToString()).SendAsync("UserOffline", userId, ct);
        await _hub.Clients.User(userId.ToString()).SendAsync("UserOffline", me.Value, ct);

        // ✅ إرسال Last Seen للمحظور (اختياري)
        var presenceService = HttpContext.RequestServices.GetRequiredService<IPresenceService>();
        var lastSeen = await presenceService.GetLastSeenAsync(new UserId(userId));
        if (lastSeen.HasValue)
        {
            await _hub.Clients.User(me.Value.ToString()).SendAsync("UserLastSeenUpdated", userId, lastSeen.Value, ct);
        }

        await _hub.Clients.User(me.Value.ToString()).SendAsync("UserBlockedByMeChanged", userId, true, ct);
        await _hub.Clients.User(userId.ToString()).SendAsync("UserBlockedMeChanged", me.Value, true, ct);

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

        // تنفيذ الأمر في الداتابيز
        await _mediator.Send(new UnblockUserCommand(me, new UserId(userId)), ct);

        // ✅ التحديث اللحظي عبر SignalR
        try
        {
            await _hub.Clients.User(me.Value.ToString()).SendAsync("CheckUserOnline", userId, ct);
            await _hub.Clients.User(userId.ToString()).SendAsync("CheckUserOnline", me.Value, ct);

            await _hub.Clients.User(me.Value.ToString()).SendAsync("UserBlockedByMeChanged", userId, false, ct);
            await _hub.Clients.User(userId.ToString()).SendAsync("UserBlockedMeChanged", me.Value, false, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR Notification Failed: {ex.Message}");
        }

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

    [HttpGet("messages/{messageId:guid}/stats")]
    [ProducesResponseType(typeof(MessageReceiptStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessageStats(
    Guid messageId,
    [FromServices] IMessageReceiptRepository receiptRepo,
    [FromServices] IUserDirectoryService userDirectory,
    CancellationToken ct)
    {
        var stats = await receiptRepo.GetMessageStatsAsync(MessageId.From(messageId), ct);

        var dto = new MessageReceiptStatsDto
        {
            TotalRecipients = stats.TotalRecipients,
            DeliveredCount = stats.DeliveredCount,
            ReadCount = stats.ReadCount,
            DeliveredUsers = await GetUserSummaries(stats.DeliveredUsers, userDirectory, ct),
            ReadUsers = await GetUserSummaries(stats.ReadUsers, userDirectory, ct)
        };

        return Ok(dto);
    }

    [HttpGet("messages/{messageId:guid}/readers-details")]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessageReadersDetails(
        Guid messageId,
        [FromServices] IMessageReceiptRepository receiptRepo,
        [FromServices] IUserDirectoryService userDirectory,
        CancellationToken ct)
    {
        var readers = await receiptRepo.GetReadersAsync(MessageId.From(messageId), ct);
        var userDtos = await GetUserDtos(readers, userDirectory, ct);
        return Ok(userDtos);
    }

    [HttpGet("messages/{messageId:guid}/delivered-details")]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessageDeliveredDetails(
        Guid messageId,
        [FromServices] IMessageReceiptRepository receiptRepo,
        [FromServices] IUserDirectoryService userDirectory,
        CancellationToken ct)
    {
        var delivered = await receiptRepo.GetDeliveredUsersAsync(MessageId.From(messageId), ct);
        var userDtos = await GetUserDtos(delivered, userDirectory, ct);
        return Ok(userDtos);
    }

    private async Task<List<UserDto>> GetUserDtos(
        IEnumerable<UserId> userIds,
        IUserDirectoryService userDirectory,
        CancellationToken ct)
    {
        var tasks = userIds.Select(id => userDirectory.GetUserAsync(id, ct));
        var users = await Task.WhenAll(tasks);
        return users.Where(u => u != null)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        DisplayName = u.DisplayName,
                        Email = u.Email,
                        IsOnline = u.IsOnline,
                        LastSeen = u.LastSeen
                    })
                    .ToList();
    }

    // ... الكود الحالي ...

    private async Task<List<UserSummaryDto>> GetUserSummaries(
        IEnumerable<UserId> userIds,
        IUserDirectoryService userDirectory,
        CancellationToken ct)
    {
        var tasks = userIds.Select(id => userDirectory.GetUserAsync(id, ct));
        var users = await Task.WhenAll(tasks);
        return users.Where(u => u != null)
                    .Select(u => new UserSummaryDto
                    {
                        Id = u.Id,
                        DisplayName = u.DisplayName,
                        IsOnline = u.IsOnline,
                        LastSeen = u.LastSeen
                    })
                    .ToList();
    }
    // في ChatController.cs أضف:
    [HttpPost("messages/{messageId:guid}/react")]
    [ProducesResponseType(typeof(MessageReactionsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReactToMessage(
        Guid messageId,
        [FromBody] ReactToMessageRequest request,
        CancellationToken ct)
    {
        if (messageId == Guid.Empty)
            return BadRequest("MessageId is required.");

        var dto = await _mediator.Send(
            new ReactToMessageCommand(
                MessageId.From(messageId),
                GetCurrentUserId(),
                request.ReactionType),
            ct);

        return Ok(dto);
    }

    [HttpGet("messages/{messageId:guid}/reactions")]
    [ProducesResponseType(typeof(MessageReactionsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessageReactions(
     Guid messageId,
     [FromServices] IReactionRepository reactionRepo,
     [FromServices] IUserDirectoryService userDirectory,
     CancellationToken ct)
    {
        if (messageId == Guid.Empty)
            return BadRequest("MessageId is required.");

        var reactions = await reactionRepo.GetForMessageAsync(MessageId.From(messageId), ct);

        var service = new ReactionsService(reactionRepo, userDirectory);
        var dto = await service.CreateReactionsDto(
            MessageId.From(messageId),
            reactions,
            GetCurrentUserId(),
            ct);

        return Ok(dto);
    }

    [HttpGet("messages/{messageId:guid}/reactions/details")]
    public async Task<IActionResult> GetReactionDetails(
    Guid messageId,
    [FromServices] IReactionRepository reactionRepo,
    [FromServices] IUserDirectoryService users,
    CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var reactions = await reactionRepo.GetForMessageAsync(MessageId.From(messageId), ct);

        var entries = new List<ReactionEntryDto>();
        foreach (var r in reactions)
        {
            var user = await users.GetUserSummaryAsync(r.UserId, ct);
            entries.Add(new ReactionEntryDto
            {
                UserId = r.UserId.Value,
                DisplayName = user?.DisplayName ?? "User",
                Type = r.Type,
                CreatedAt = r.CreatedAt,
                IsMe = r.UserId == currentUserId
            });
        }

        // ✅ "You" أول واحد
        entries = entries
            .OrderByDescending(e => e.IsMe)
            .ThenByDescending(e => e.CreatedAt)
            .ToList();

        // ✅ بناء الـ Tabs
        var tabs = new List<ReactionTabDto> { new() { Type = null, Count = entries.Count } };
        tabs.AddRange(
            reactions
                .GroupBy(r => r.Type)
                .Select(g => new ReactionTabDto { Type = g.Key, Count = g.Count() })
                .OrderByDescending(t => t.Count)
        );

        return Ok(new MessageReactionsDetailsDto
        {
            MessageId = messageId,
            CurrentUserId = currentUserId.Value,
            Tabs = tabs,
            Entries = entries
        });
    }



    [HttpDelete("messages/{messageId:guid}/reactions/me")]
    public async Task<IActionResult> RemoveMyReaction(
     Guid messageId,
     [FromServices] IReactionRepository reactionRepo,
     [FromServices] IUnitOfWork uow,
     CancellationToken ct)
    {
        var reaction = await reactionRepo.GetAsync(
            MessageId.From(messageId),
            GetCurrentUserId(),
            ct);

        if (reaction is null)
            return NoContent();

        await reactionRepo.RemoveAsync(reaction, ct);
        await uow.CommitAsync(ct);

        return NoContent();
    }

    [HttpPatch("messages/{messageId:guid}")]
    public async Task<IActionResult> EditMessage(Guid messageId, [FromBody] string newContent, CancellationToken ct)
    {
        // شيل الـ .Value من GetCurrentUserId()
        await _mediator.Send(new EditMessageCommand(messageId, GetCurrentUserId(), newContent), ct);
        return NoContent();
    }
    [HttpDelete("messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid messageId, [FromQuery] bool deleteForEveryone, CancellationToken ct)
    {
        // شيل الـ .Value من GetCurrentUserId()
        await _mediator.Send(new DeleteMessageCommand(messageId, GetCurrentUserId(), deleteForEveryone), ct);
        return NoContent();
    }
    // EnterpriseChat.API/Controllers/ChatController.cs
    // EnterpriseChat.API/Controllers/ChatController.cs

    [HttpGet("rooms/{roomId}/messages/search")]
    public async Task<ActionResult<IReadOnlyList<MessageReadDto>>> Search(
        [FromRoute] Guid roomId,
        [FromQuery] string query,
        [FromQuery] int take = 50)
    {
        var userId = User.GetUserId(); // تأكد أنها ترجع Guid
        if (userId == null) return Unauthorized();

        return Ok(await _mediator.Send(new SearchMessagesQuery(roomId, userId.Value, query, take)));
    }
    [HttpPost("messages/forward")]
    public async Task<ActionResult> Forward([FromBody] ForwardMessagesRequest request)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _mediator.Send(new ForwardMessagesCommand(
            userId.Value,
            request.MessageIds,
            request.TargetRoomIds));

        return result ? Ok() : BadRequest("Forward failed");
    }

    
}