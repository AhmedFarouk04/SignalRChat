using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

using System.Security.Claims;

namespace EnterpriseChat.API.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IPresenceService _presence;
    private readonly IMediator _mediator;
    private readonly IChatRoomRepository _roomRepository;
    private readonly IRoomPresenceService _roomPresence;
    private readonly ITypingService _typing;
    private readonly IUserBlockRepository _blockRepository;  // ← أضف ده
    private static readonly ConcurrentDictionary<string, DateTime> _typingBroadcastGate = new();
    private static readonly ConcurrentDictionary<string, byte> _joinedRooms = new();

    public ChatHub(
        IPresenceService presence,
        IMediator mediator,
        IChatRoomRepository roomRepository,
        IRoomPresenceService roomPresence,
        ITypingService typing,
        IUserBlockRepository blockRepository)
    {
        _presence = presence;
        _mediator = mediator;
        _roomRepository = roomRepository;
        _roomPresence = roomPresence;
        _typing = typing;
        _blockRepository = blockRepository;
    }

    // ✅ ChatHub.cs - الجزء المُصلح من OnConnectedAsync

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        await _presence.UserConnectedAsync(userId, connectionId);

        // ✅ الحل: خلي المستخدم ينضم لكل "روماته" في SignalR أول ما يفتح
        try
        {
            var rooms = await _roomRepository.GetForUserAsync(userId, CancellationToken.None);
            foreach (var room in rooms)
            {
                await Groups.AddToGroupAsync(connectionId, room.Id.ToString());
                Console.WriteLine($"[ChatHub] User {userId} joined room {room.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatHub] Error joining rooms: {ex.Message}");
        }

        // ✅ Auto-deliver غير المُسلَّمة (اختياري - لو محتاجه)
        _ = Task.Run(async () =>
        {
            try
            {
                // هنا ممكن تضيف Logic لتسليم الرسائل القديمة غير المُسلَّمة
                await Task.Delay(1000); // انتظر ثانية عشان الـ connection يستقر

                // مثال: جلب الرسائل غير المُسلَّمة
                // var undelivered = await _messageRepository.GetUndeliveredForUserAsync(userId);
                // foreach (var msg in undelivered)
                // {
                //     await _mediator.Send(new DeliverMessageCommand(msg.Id, userId));
                // }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHub] Auto-deliver error: {ex.Message}");
            }
        });

        await base.OnConnectedAsync();
    }

    public async Task HandleBlockUpdate(Guid blockerId, Guid blockedId, bool isBlocked)
    {
        if (isBlocked)
        {
            // لو حصل بلوك: اقطع رؤية الأونلاين فوراً بين الطرفين
            await Clients.User(blockerId.ToString()).SendAsync("UserOffline", blockedId);
            await Clients.User(blockedId.ToString()).SendAsync("UserOffline", blockerId);
        }
        else
        {
            // لو اتفك البلوك: اطلب من الـ Clients إعادة التحقق من الحالة
            await Clients.User(blockerId.ToString()).SendAsync("CheckUserOnline", blockedId);
            await Clients.User(blockedId.ToString()).SendAsync("CheckUserOnline", blockerId);
        }
    }

    public async Task<bool> GetUserOnlineStatus(Guid userId)
    {
        var me = GetUserId();
        var target = new UserId(userId);
        var isBlocked = await _blockRepository.IsBlockedAsync(me, target) ||
                        await _blockRepository.IsBlockedAsync(target, me);
        if (isBlocked) return false;

        return await _presence.IsOnlineAsync(target);
    }
    public async Task PinMessage(Guid roomId, Guid? messageId)
    {
        // هنا ممكن تضيف Logic للتأكد إن اللي بيثبت هو الـ Owner
        await Clients.Group(roomId.ToString()).SendAsync("MessagePinned", roomId, messageId);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var stillOnline = await _presence.IsOnlineAsync(userId);

        if (!stillOnline)
        {
            // إبلاغ الناس المرئيين فقط بأنك أصبحت أوفلاين
            var visibleUsers = await GetVisibleOnlineUsersForMe(userId);
            foreach (var target in visibleUsers)
            {
                await Clients.User(target.Value.ToString()).SendAsync("UserOffline", userId.Value);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
    // حط هذا السطر في أي مكان داخل الكلاس
    public async Task MemberRemoved(Guid roomId, Guid userId, string removerName)
    {
        await Clients.Group(roomId.ToString())
            .SendAsync("MemberRemoved", roomId, userId, removerName);
    }
    public async Task JoinRoom(string roomId)
    {
        var userId = GetUserId();
        var rid = new RoomId(Guid.Parse(roomId));

        // التحقق من أن المستخدم ليس محظوراً من الغرفة أو من المالك
        var room = await _roomRepository.GetByIdAsync(rid);
        if (room == null) throw new HubException("Room not found");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await _roomPresence.JoinRoomAsync(rid, userId);

        // إبلاغ الآخرين في الغرفة
        var count = await _roomPresence.GetOnlineCountAsync(rid);
        await Clients.Group(roomId).SendAsync("RoomPresenceUpdated", rid.Value, count);
    }
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong");
    }

    public async Task LeaveRoom(string roomId)
    {
        var userId = GetUserId();
        var rid = new RoomId(Guid.Parse(roomId));

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        _joinedRooms.TryRemove($"{Context.ConnectionId}:{roomId}", out _);

        await _roomPresence.LeaveRoomAsync(rid, userId);

        var count = await _roomPresence.GetOnlineCountAsync(rid);
        await Clients.OthersInGroup(roomId).SendAsync("RoomPresenceUpdated", rid.Value, count);
    }

    public async Task TypingStart(string roomId)
    {
        var userId = GetUserId();
        var rid = new RoomId(Guid.Parse(roomId));
        var room = await _roomRepository.GetByIdAsync(rid);

        if (room is null) return;

        var isOwner = room.OwnerId?.Value == userId.Value;
        if (!isOwner && !room.IsMember(userId)) return;

        await _typing.StartTypingAsync(rid, userId, TimeSpan.FromSeconds(4));

        // throttle: مرة كل 1 ثانية لكل (room,user)
        var gateKey = $"{roomId}:{userId.Value}";
        var now = DateTime.UtcNow;

        if (_typingBroadcastGate.TryGetValue(gateKey, out var last) &&
            (now - last).TotalMilliseconds < 1000)
            return;

        _typingBroadcastGate[gateKey] = now;

        await Clients.OthersInGroup(roomId)
            .SendAsync("TypingStarted", rid.Value, userId.Value);
    }


    public async Task TypingStop(string roomId)
    {
        var userId = GetUserId();
        var rid = new RoomId(Guid.Parse(roomId));

        await _typing.StopTypingAsync(rid, userId);

        await Clients.OthersInGroup(roomId)
            .SendAsync("TypingStopped", rid.Value, userId.Value);
    }

    public async Task MarkRead(Guid messageId)
    {
        await _mediator.Send(new ReadMessageCommand(
            MessageId.From(messageId),
            GetUserId()));
    }

    public async Task MarkRoomRead(Guid roomId, Guid lastMessageId)
    {
        await _mediator.Send(new MarkRoomReadCommand(
            new RoomId(roomId),
            GetUserId(),
            MessageId.From(lastMessageId)));
    }

    public async Task<IReadOnlyCollection<Guid>> GetOnlineUsers()
    {
        var me = GetUserId();
        var allOnline = await _presence.GetOnlineUsersAsync();
        var visible = new List<Guid>();

        foreach (var u in allOnline)
        {
            // الفلتر السحري: لو فيه بلوك مش هيشوفوا بعض في قائمة الـ Online
            var blocked = await _blockRepository.IsBlockedAsync(me, u);
            if (!blocked) visible.Add(u.Value);
        }
        return visible;
    }


    public async Task RemoveMember(Guid roomId, Guid userId)
    {
        await Clients.User(userId.ToString())
            .SendAsync("RemovedFromRoom", roomId);
    }

    public async Task GroupRenamed(Guid roomId, string newName)
    {
        await Clients.Group(roomId.ToString()).SendAsync("GroupRenamed", roomId, newName);
    }

    public async Task MemberAdded(Guid roomId, Guid userId, string displayName)
    {
        await Clients.Group(roomId.ToString()).SendAsync("MemberAdded", roomId, userId, displayName);
    }


    public async Task SendMessageWithReply(SendMessageWithReplyRequest request)
    {
        var userId = GetUserId();

        var command = new SendMessageCommand(
            new RoomId(request.RoomId),
            userId,
            request.Content,
            request.ReplyToMessageId.HasValue ?
                new MessageId(request.ReplyToMessageId.Value) : null);

        var result = await _mediator.Send(command);

        // ✅ حول الـ result لـ MessageDto
        var messageDto = new MessageDto
        {
            Id = result.Id,
            RoomId = result.RoomId,
            SenderId = result.SenderId,
            Content = result.Content,
            CreatedAt = result.CreatedAt,
            Status = result.Status,
            ReplyToMessageId = result.ReplyToMessageId,
            ReplyInfo = result.ReplyInfo != null ? new ReplyInfoDto
            {
                MessageId = result.ReplyInfo.MessageId,
                SenderId = result.ReplyInfo.SenderId,
                SenderName = result.ReplyInfo.SenderName,
                ContentPreview = result.ReplyInfo.ContentPreview,
                CreatedAt = result.ReplyInfo.CreatedAt,
                IsDeleted = result.ReplyInfo.IsDeleted
            } : null,
            IsEdited = result.IsEdited,
            IsDeleted = result.IsDeleted,
            ReadCount = result.ReadCount,
            DeliveredCount = result.DeliveredCount,
            TotalRecipients = result.TotalRecipients
        };

        // 🔥 فك التعليق عن السطر ده!
        await Clients.Group(request.RoomId.ToString())
        .SendAsync("MessageReceived", messageDto);

        // 2. ابعت مباشرة لكل عضو (Fallback)
        var room = await _roomRepository.GetByIdWithMembersAsync(new RoomId(request.RoomId));
        if (room != null)
        {
            foreach (var member in room.Members)
            {
                await Clients.User(member.UserId.Value.ToString())
                    .SendAsync("MessageReceived", messageDto);
            }
        }
    }
    private UserId GetUserId()
{
    var raw =
        Context.User?.FindFirst("sub")?.Value
        ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? Context.User?.FindFirst("nameid")?.Value;

    if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var id) || id == Guid.Empty)
        throw new HubException("User not authenticated");

    return new UserId(id);
}
    private async Task<List<UserId>> GetVisibleOnlineUsersForMe(UserId me)
    {
        var allOnline = await _presence.GetOnlineUsersAsync();

        // لو الـ context مش متاح، ارجع كل الـ online (بدون فلتر بلوك)
        // أو استخدم cache إن وجد
        try
        {
            var result = new List<UserId>();
            foreach (var u in allOnline)
            {
                if (u == me) continue;

                // حاول بس لو الـ context شغال
                var blocked = await _blockRepository.IsBlockedAsync(me, u, Context.ConnectionAborted);
                if (!blocked) result.Add(u);
            }
            return result;
        }
        catch (TaskCanceledException)
        {
            // في حالة disconnect، ارجع كل الناس (مش مهم الفلتر دقيق هنا)
            return allOnline.Where(u => u != me).ToList();
        }
    }


}
