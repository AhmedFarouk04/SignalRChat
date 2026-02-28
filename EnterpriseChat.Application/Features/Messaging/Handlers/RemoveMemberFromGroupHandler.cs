using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class RemoveMemberFromGroupHandler
    : IRequestHandler<RemoveMemberFromGroupCommand, Unit>
{
    private readonly IChatRoomRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IMessageRepository _messages;
    private readonly IUserLookupService _users;

    public RemoveMemberFromGroupHandler(
        IChatRoomRepository repo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster,
        IMessageRepository messages,
        IUserLookupService users)
    {
        _repo = repo;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
        _messages = messages;
        _users = users;
    }

    public async Task<Unit> Handle(RemoveMemberFromGroupCommand command, CancellationToken ct)
    {
        // --- التحقق من الصلاحيات والمدخلات (نفس الكود) ---
        if (command.RoomId.Value == Guid.Empty)
            throw new ArgumentException("RoomId is required.");
        if (command.MemberId.Value == Guid.Empty)
            throw new ArgumentException("MemberId cannot be empty.");
        var room = await _repo.GetByIdWithMembersAsync(command.RoomId, ct)
            ?? throw new InvalidOperationException("Room not found.");
        if (room.Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms allowed.");
        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.RequesterId, ct);
        if (room.OwnerId == command.MemberId)
            throw new InvalidOperationException("Owner cannot be removed.");
        if (room.Members.Count <= 1)
            throw new InvalidOperationException("Cannot remove the last member of the group.");
        var target = room.Members.FirstOrDefault(m => m.UserId == command.MemberId);
        if (target is null)
            throw new InvalidOperationException("Member not found in this group.");
        var requesterIsOwner = room.OwnerId == command.RequesterId;
        if (!requesterIsOwner)
        {
            await _auth.EnsureUserIsAdminAsync(command.RoomId, command.RequesterId, ct);
            if (target.IsAdmin)
                throw new UnauthorizedAccessException("Only owner can remove admins.");
        }
        // --- نهاية التحقق ---

        // 1. تنفيذ العملية (إزالة العضو)
        room.RemoveMember(command.MemberId);
        await _uow.CommitAsync(ct);

        var recipients = room.GetMemberIds().DistinctBy(x => x.Value).ToList();

        var removedName = await _users.GetDisplayNameAsync(command.MemberId.Value, ct) ?? "Someone";
        var requesterName = await _users.GetDisplayNameAsync(command.RequesterId.Value, ct) ?? "Someone";

        // 2. إنشاء وحفظ رسالة النظام
        var systemText = command.MemberId.Value == command.RequesterId.Value
            ? $"{removedName} left the group"
            : $"{removedName} was removed by {requesterName}";

        var sysMsg = Message.CreateSystemMessage(
            room.Id,
            systemText,
            SystemMessageType.MemberRemoved,
            recipients); // هذا الـ recipients مهم لتحديد من يستلم الرسالة داخل الشات

        await _messages.AddAsync(sysMsg, ct);
        await _uow.CommitAsync(ct);

        // 3. بث رسالة النظام داخل الشات فقط (Chat Area)
        var msgDto = new MessageDto
        {
            Id = sysMsg.Id.Value,
            RoomId = room.Id.Value,
            SenderId = UserId.System.Value,
            Content = systemText,
            CreatedAt = sysMsg.CreatedAt,
            IsSystemMessage = true
        };
        await _broadcaster.BroadcastMessageAsync(msgDto, recipients);

        // 4. [الأهم] تحديث قائمة الغرف (Room List) بدون نص معاينة (Preview)
        //    - نرسل نفس الـ recipients (كل أعضاء المجموعة)
        var listDto = new RoomListItemDto
        {
            Id = room.Id.Value,
            Name = room.Name,
            Type = room.Type.ToString(),
            UnreadCount = 0, // رسائل النظام لا تزيد عداد unread في القائمة (حسب متطلباتك)
            IsMuted = false, // يتم جلب حالة الكتم الحقيقية من قاعدة البيانات
            LastMessageAt = sysMsg.CreatedAt,
            // ✨✨ الحل السحري هنا ✨✨
            LastMessagePreview = null, // null => لا يظهر أي نص في الـ Preview
            LastMessageId = sysMsg.Id.Value,
            LastMessageSenderId = UserId.System.Value,
            // بما أن LastMessagePreview = null، LastMessageStatus يمكن أن يكون null أو أي قيمة محايدة
            LastMessageStatus = null
        };
        await _broadcaster.RoomUpsertedAsync(listDto, recipients);

        // 5. إشعار شخصي للعضو الذي تمت إزالته (هذا مهم)
        await _broadcaster.MemberRemovedAsync(
            room.Id,
            command.MemberId,
            command.RequesterId,
            requesterName,
            new List<UserId> { command.MemberId });

        return Unit.Value;
    }
}