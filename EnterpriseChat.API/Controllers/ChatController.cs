using EnterpriseChat.API.Contracts.Messaging;
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Features.Messaging.Handlers;
using EnterpriseChat.Application.Features.Messaging.Queries;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseChat.API.Controllers;
[Authorize]
[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly SendMessageCommandHandler _sendMessageHandler;
    private readonly GetMessagesQueryHandler _getMessagesHandler;
	private readonly GetMessageReadersQueryHandler _getMessageReaders;
    private readonly CreateGroupChatHandler _createGroupHandler;
    private readonly AddMemberToGroupHandler _addMemberHandler;
    private readonly RemoveMemberFromGroupHandler _removeMemberHandler;
    private readonly GetOrCreatePrivateRoomHandler _getOrCreatePrivateRoomHandler;
    private readonly BlockUserCommandHandler _blockUserHandler;
    private readonly MuteRoomCommandHandler _muteRoomHandler;
    private readonly UnmuteRoomCommandHandler _unmuteRoomHandler;
    private readonly IChatRoomRepository _roomRepository;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly GetMyRoomsQueryHandler _getMyRooms;


    public ChatController(
    SendMessageCommandHandler sendMessageHandler,
    GetMessagesQueryHandler getMessagesHandler,
    GetMessageReadersQueryHandler getMessageReaders,
    CreateGroupChatHandler createGroupHandler,
    AddMemberToGroupHandler addMemberHandler,
    RemoveMemberFromGroupHandler removeMemberHandler,
    GetOrCreatePrivateRoomHandler getOrCreatePrivateRoomHandler
       , BlockUserCommandHandler blockUserHandler,
MuteRoomCommandHandler muteRoomHandler,
UnmuteRoomCommandHandler unmuteRoomHandler,
 IChatRoomRepository roomRepository,
    IMessageBroadcaster broadcaster, 
GetMyRoomsQueryHandler getMyRooms)

    {
        _sendMessageHandler = sendMessageHandler;
        _getMessagesHandler = getMessagesHandler;
        _getMessageReaders = getMessageReaders;
        _createGroupHandler = createGroupHandler;
        _addMemberHandler = addMemberHandler;
        _removeMemberHandler = removeMemberHandler;
        _getOrCreatePrivateRoomHandler = getOrCreatePrivateRoomHandler;
        _blockUserHandler = blockUserHandler;
        _muteRoomHandler = muteRoomHandler;
        _unmuteRoomHandler = unmuteRoomHandler;
        _roomRepository = roomRepository;               // ✅
        _broadcaster = broadcaster;
        _getMyRooms = getMyRooms;
    }


    [HttpPost("messages")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Content.Length > 2000)
            return BadRequest("Message too long.");

        if (request.RoomId == Guid.Empty)
            return BadRequest("RoomId is required.");

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Message content is required.");

        var senderId = GetCurrentUserId();

        var command = new SendMessageCommand(
            new RoomId(request.RoomId),
            senderId,
            request.Content
        );


        var result = await _sendMessageHandler.Handle(command, cancellationToken);

        var room = await _roomRepository.GetByIdAsync(
            new RoomId(request.RoomId), cancellationToken);

        var recipients = room!.Members
            .Select(m => m.UserId)
            .Where(x => x != senderId);

        await _broadcaster.BroadcastMessageAsync(result, recipients);

        return Ok(result);
    }


    [HttpGet("rooms/{roomId}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageReadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(
    Guid roomId,
    [FromQuery] int skip = 0,
    [FromQuery] int take = 50,
    CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty)
            return BadRequest("RoomId is required.");

        var query = new GetMessagesQuery(
     new RoomId(roomId),
     GetCurrentUserId(), // ✅
     skip,
     take);


        var result = await _getMessagesHandler.Handle(query, cancellationToken);
        return Ok(result);
    }
    private UserId GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim))
            throw new UnauthorizedAccessException("User not authenticated");

        return new UserId(Guid.Parse(userIdClaim));
    }
	[HttpGet("messages/{messageId}/readers")]
	[ProducesResponseType(typeof(IReadOnlyList<MessageReadReceiptDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetReaders(
	Guid messageId,
	CancellationToken cancellationToken)
	{
		var query = new GetMessageReadersQuery(
			MessageId.From(messageId));

		var result = await _getMessageReaders.Handle(
			query,
			cancellationToken);

		return Ok(result);
	}
    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup(
    [FromBody] CreateGroupRequest request,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Group name is required.");

        var creatorId = GetCurrentUserId();

        var members = request.Members
            .Select(id => new UserId(id))
            .ToList();

        var command = new CreateGroupChatCommand(
            request.Name,
            creatorId,
            members);

        var room = await _createGroupHandler.Handle(command, cancellationToken);

        return Ok(new
        {
            room.Id,
            room.Name,
            room.Type
        });
    }


    [HttpPost("groups/{roomId}/members/{userId}")]
    public async Task<IActionResult> AddMember(
    Guid roomId,
    Guid userId,
    CancellationToken ct)
    {
        await _addMemberHandler.Handle(
    new AddMemberToGroupCommand(
        new RoomId(roomId),
        new UserId(userId),
        GetCurrentUserId()), // ✅
    ct);

        return NoContent();
    }
    [HttpDelete("groups/{roomId}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(
        Guid roomId,
        Guid userId,
        CancellationToken ct)
    {
        await _removeMemberHandler.Handle(
     new RemoveMemberFromGroupCommand(
         new RoomId(roomId),
         new UserId(userId),
         GetCurrentUserId()),
     ct);


        return NoContent();
    }


    [HttpPost("private/{userId}")]
    public async Task<IActionResult> GetOrCreatePrivateChat(
    Guid userId,
    CancellationToken ct)
    {
        var me = GetCurrentUserId();
        var other = new UserId(userId);

        var room = await _getOrCreatePrivateRoomHandler
            .Handle(me, other, ct);

        return Ok(new
        {
            room.Id,
            room.Type
        });
    }

    [HttpPost("block/{userId}")]
    public async Task<IActionResult> BlockUser(
    Guid userId,
    CancellationToken ct)
    {
        var me = GetCurrentUserId();
        var other = new UserId(userId);

        await _blockUserHandler.Handle(
            new BlockUserCommand(me, other),
            ct);

        return NoContent();
    }
    [HttpPost("mute/{roomId}")]
    public async Task<IActionResult> MuteRoom(
        Guid roomId,
        CancellationToken ct)
    {
        await _muteRoomHandler.Handle(
            new MuteRoomCommand(
                new RoomId(roomId),
                GetCurrentUserId()),
            ct);

        return NoContent();
    }
    [HttpDelete("mute/{roomId}")]
    public async Task<IActionResult> UnmuteRoom(
        Guid roomId,
        CancellationToken ct)
    {
        await _unmuteRoomHandler.Handle(
            new UnmuteRoomCommand(
                new RoomId(roomId),
                GetCurrentUserId()),
            ct);

        return NoContent();
    }


    [HttpGet("rooms/{roomId}/online-users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOnlineUsersInRoom(Guid roomId)
    {
        if (roomId == Guid.Empty)
            return BadRequest("RoomId is required.");

        // مؤقتًا (UI-focused)
        // هنرجّع بيانات بسيطة جاهزة للـ UI
        // بعدين نربطها بالـ PresenceService الحقيقي

        var users = new[]
        {
        new { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), DisplayName = "Ahmed" },
        new { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), DisplayName = "Ali" }
    };

        return Ok(users);
    }


    [HttpGet("rooms/{roomId}")]
    public async Task<IActionResult> GetRoom(Guid roomId)
    {
        var room = await _roomRepository.GetByIdAsync(new RoomId(roomId));

        if (room == null)
            return NotFound();

        // private room + user deleted
        if (room.Type == RoomType.Private /* && room.IsOtherUserDeleted()*/)
        {
            return Ok(new
            {
                Id = roomId,
                Type = "Private",
                IsDeleted = true
            });
        }

        return Ok(new
        {
            Id = room.Id.Value,
            Name = room.Name,
            Type = room.Type.ToString()
        });
    }




    [HttpGet("rooms")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyRooms(CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        var result = await _getMyRooms.Handle(
            new GetMyRoomsQuery(userId),
            ct);

        return Ok(result);
    }


    [HttpGet("groups/{roomId}/members")]
    public async Task<IActionResult> GetGroupMembers(
    Guid roomId,
    CancellationToken ct)
    {
        var room = await _roomRepository.GetByIdAsync(
            new RoomId(roomId), ct);

        if (room is null)
            return NotFound();

        var me = GetCurrentUserId();

        if (!room.Members.Any(m => m.UserId == me))
            return Forbid();


        return Ok(new
        {
            OwnerId = room.OwnerId.Value,
            Members = room.Members.Select(m => new
            {
                Id = m.UserId.Value,
                DisplayName = $"User {m.UserId.Value.ToString()[..6]}"
            })
        });

    }



}
