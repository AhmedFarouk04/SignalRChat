// EnterpriseChat.Application/Features/Messaging/Handlers/ReactToMessageCommandHandler.cs
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class ReactToMessageCommandHandler
    : IRequestHandler<ReactToMessageCommand, MessageReactionsDto>
{
    private readonly IReactionRepository _reactionRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IUnitOfWork _uow;
    private readonly IUserDirectoryService _userDirectory;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IChatRoomRepository _roomRepository;

    public ReactToMessageCommandHandler(
    IReactionRepository reactionRepo,
    IMessageRepository messageRepo,
    IUnitOfWork uow,
    IUserDirectoryService userDirectory,
    IMessageBroadcaster broadcaster,
    IChatRoomRepository roomRepository) // ← أضف ده
    {
        _reactionRepo = reactionRepo;
        _messageRepo = messageRepo;
        _uow = uow;
        _userDirectory = userDirectory;
        _broadcaster = broadcaster;
        _roomRepository = roomRepository; // ← أضف ده
    }

    public async Task<MessageReactionsDto> Handle(ReactToMessageCommand request, CancellationToken ct)
    {
        // تحقق من وجود الرسالة
        var message = await _messageRepo.GetByIdAsync(request.MessageId.Value, ct);
        if (message is null)
            throw new InvalidOperationException("Message not found");

        // ابحث عن reaction موجود
        var existingReaction = await _reactionRepo.GetAsync(request.MessageId, request.UserId, ct);

        if (existingReaction is not null)
        {
            if (existingReaction.Type == request.ReactionType)
            {
                // نفس الـ reaction → احذفه (toggle)
                await _reactionRepo.RemoveAsync(existingReaction, ct);
            }
            else
            {
                // reaction مختلف → عدله
                existingReaction.UpdateType(request.ReactionType);
                await _reactionRepo.UpdateAsync(existingReaction, ct);
            }
        }
        else
        {
            // reaction جديد
            var newReaction = new Reaction(request.MessageId, request.UserId, request.ReactionType);
            await _reactionRepo.AddAsync(newReaction, ct);
        }

        await _uow.CommitAsync(ct);

        // أحصل على كل reactions للرسالة
        var reactions = await _reactionRepo.GetForMessageAsync(request.MessageId, ct);

        // أنشئ الـ DTO
        var dto = await CreateReactionsDto(request.MessageId, reactions, request.UserId, ct);

        // أرسل تحديث real-time
        bool isNewReaction;
        if (existingReaction is null)
            isNewReaction = true;  // reaction جديد
        else if (existingReaction.Type == request.ReactionType)
            isNewReaction = false; // حذف (toggle off)
        else
            isNewReaction = true;  // تغيير النوع = new reaction من نوع تاني

        await _broadcaster.MessageReactionUpdatedAsync(
            request.MessageId,
            request.UserId,
            request.ReactionType,
            isNewReaction,
            await GetRoomMemberIds(message.RoomId, ct));
        // ابعت RoomUpdated للـ members بـ preview خاص بالـ reaction
        var memberIds = await GetRoomMemberIds(message.RoomId, ct);
        var senderOfMessage = message.SenderId;

        // الـ preview بيظهر بس لصاحب الرسالة
        var reactor = await _userDirectory.GetUserSummaryAsync(request.UserId, ct);
        var emoji = GetEmoji(request.ReactionType);
        var preview = isNewReaction
            ? $"{reactor?.DisplayName ?? "Someone"} reacted {emoji} to your message"
            : null;

        if (preview != null)
        {
            var roomUpdate = new RoomUpdatedDto
            {
                RoomId = message.RoomId.Value,
                MessageId = message.Id.Value,
                SenderId = request.UserId.Value,
                Preview = preview,
                CreatedAt = DateTime.UtcNow,
                UnreadDelta = 0
            };

            // ابعت بس لصاحب الرسالة مش للكل
            await _broadcaster.RoomUpdatedAsync(
                roomUpdate,
                new List<UserId> { senderOfMessage });
        }
        // ✅ احفظ الـ reaction preview في الداتابيز عشان يظهر بعد الـ refresh
        var room = await _roomRepository.GetByIdWithMembersAsync(message.RoomId, ct);
        if (room != null)
        {
            if (isNewReaction && preview != null)
            {
                // ✅ احفظ الـ reaction preview
                room.SetLastReactionPreview(preview, DateTime.UtcNow, senderOfMessage);
            }
            else if (!isNewReaction)
            {
                // ✅ امسح الـ reaction preview لما بيتحذف
                room.ClearLastReactionPreview();
            }

            await _uow.CommitAsync(ct);
        }
        return dto;
    }

    private async Task<MessageReactionsDto> CreateReactionsDto(
        MessageId messageId,
        IReadOnlyList<Reaction> reactions,
        UserId currentUserId,
        CancellationToken ct)
    {
        var dto = new MessageReactionsDto
        {
            MessageId = messageId.Value
        };

        // حساب الـ counts
        foreach (var reaction in reactions)
        {
            if (!dto.Counts.ContainsKey(reaction.Type))
                dto.Counts[reaction.Type] = 0;

            dto.Counts[reaction.Type]++;

            // إضافة المستخدمين لكل نوع
            if (!dto.UsersByType.ContainsKey(reaction.Type))
                dto.UsersByType[reaction.Type] = new List<UserSummaryDto>();

            var user = await _userDirectory.GetUserSummaryAsync(reaction.UserId, ct);
            if (user is not null)
                dto.UsersByType[reaction.Type].Add(user);

            // reaction الحالي للمستخدم
            if (reaction.UserId == currentUserId)
            {
                dto.CurrentUserReaction = reaction.Id.Value;
                dto.CurrentUserReactionType = reaction.Type;
            }
        }

        return dto;
    }
    private static string GetEmoji(ReactionType type) => type switch
    {
        ReactionType.Like => "👍",
        ReactionType.Love => "❤️",
        ReactionType.Laugh => "😂",
        ReactionType.Wow => "😮",
        ReactionType.Sad => "😢",
        ReactionType.Angry => "😠",
        ReactionType.ThumbsDown => "👎",
        ReactionType.Fire => "🔥",
        ReactionType.Party => "🎉",
        ReactionType.Clap => "👏",
        ReactionType.Pray => "🙏",
        _ => "❓"
    };

    
    private async Task<IReadOnlyList<UserId>> GetRoomMemberIds(RoomId roomId, CancellationToken ct)
    {
        var room = await _roomRepository.GetByIdWithMembersAsync(roomId, ct);
        if (room is null) return new List<UserId>();

        return room.Members
            .Select(m => m.UserId)
            .ToList();
    }
}