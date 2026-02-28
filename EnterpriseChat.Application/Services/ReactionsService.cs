// EnterpriseChat.Application/Services/ReactionsService.cs
using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Services;

public sealed class ReactionsService
{
    private readonly IReactionRepository _reactionRepo;
    private readonly IUserDirectoryService _userDirectory;

    public ReactionsService(
        IReactionRepository reactionRepo,
        IUserDirectoryService userDirectory)
    {
        _reactionRepo = reactionRepo;
        _userDirectory = userDirectory;
    }

    public async Task<MessageReactionsDto> CreateReactionsDto(
        MessageId messageId,
        IReadOnlyList<Reaction> reactions,
        UserId currentUserId,
        CancellationToken ct)
    {
        var dto = new MessageReactionsDto
        {
            MessageId = messageId.Value
        };

        foreach (var reaction in reactions)
        {
            if (!dto.Counts.ContainsKey(reaction.Type))
                dto.Counts[reaction.Type] = 0;

            dto.Counts[reaction.Type]++;

            if (!dto.UsersByType.ContainsKey(reaction.Type))
                dto.UsersByType[reaction.Type] = new List<UserSummaryDto>();

            var user = await _userDirectory.GetUserSummaryAsync(reaction.UserId, ct);
            if (user is not null)
                dto.UsersByType[reaction.Type].Add(user);

            if (reaction.UserId == currentUserId)
            {
                dto.CurrentUserReaction = reaction.Id.Value;
                dto.CurrentUserReactionType = reaction.Type;
            }
        }

        return dto;
    }
    public async Task<MessageReactionsDetailsDto> CreateReactionsDetailsDto(
    MessageId messageId,
    IReadOnlyList<Reaction> reactions,
    UserId currentUserId,
    CancellationToken ct)
    {
        var dto = new MessageReactionsDetailsDto
        {
            MessageId = messageId.Value,
            CurrentUserId = currentUserId.Value
        };

        // Tabs
        dto.Tabs.Add(new ReactionTabDto
        {
            Type = null,
            Count = reactions.Count
        });

        foreach (var g in reactions.GroupBy(r => r.Type))
        {
            dto.Tabs.Add(new ReactionTabDto
            {
                Type = g.Key,
                Count = g.Count()
            });
        }

        // Entries
        foreach (var reaction in reactions)
        {
            var user = await _userDirectory.GetUserSummaryAsync(reaction.UserId, ct);
            dto.Entries.Add(new ReactionEntryDto
            {
                UserId = reaction.UserId.Value,
                DisplayName = user?.DisplayName ?? "Unknown",
                Type = reaction.Type,
                CreatedAt = reaction.CreatedAt,
                IsMe = reaction.UserId == currentUserId
            });
        }

        return dto;
    }

}