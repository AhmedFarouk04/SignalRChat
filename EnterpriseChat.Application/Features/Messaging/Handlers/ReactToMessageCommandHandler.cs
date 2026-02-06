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

    public ReactToMessageCommandHandler(
        IReactionRepository reactionRepo,
        IMessageRepository messageRepo,
        IUnitOfWork uow,
        IUserDirectoryService userDirectory,
        IMessageBroadcaster broadcaster)
    {
        _reactionRepo = reactionRepo;
        _messageRepo = messageRepo;
        _uow = uow;
        _userDirectory = userDirectory;
        _broadcaster = broadcaster;
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
        await _broadcaster.MessageReactionUpdatedAsync(
            request.MessageId,
            request.UserId,
            request.ReactionType,
            existingReaction is null,
            await GetRoomMemberIds(message.RoomId, ct));

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

    private async Task<IReadOnlyList<UserId>> GetRoomMemberIds(RoomId roomId, CancellationToken ct)
    {
        // هنا تحتاج لـ method للحصول على أعضاء الغرفة
        // سنضيفها لاحقاً أو يمكن استخدام ChatRoomRepository
        return new List<UserId>();
    }
}