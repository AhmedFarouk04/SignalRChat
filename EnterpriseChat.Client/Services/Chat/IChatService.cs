using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.Client.Services.Chat;

public interface IChatService
{
    Task<IReadOnlyList<MessageModel>> GetMessagesAsync(Guid roomId, int skip = 0, int take = 50);
    Task<MessageDto?> SendMessageAsync(Guid roomId, string content);
    Task<IReadOnlyList<MessageReadReceiptDto>> GetReadersAsync(Guid messageId);
    Task StartTypingAsync(Guid roomId, int ttlSeconds = 5, CancellationToken ct = default);
    Task StopTypingAsync(Guid roomId, CancellationToken ct = default);
    Task<GroupMembersDto?> GetGroupMembersAsync(Guid roomId);

    Task MuteAsync(Guid roomId);
    Task UnmuteAsync(Guid roomId);
    Task BlockUserAsync(Guid userId);
    Task RemoveMemberAsync(Guid roomId, Guid userId);
    Task MarkMessageDeliveredAsync(Guid messageId);
    Task MarkMessageReadAsync(Guid messageId);
    Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId);
    // في IChatService.cs أضف:
    // في IChatService.cs
    Task<MessageReceiptStatsDto?> GetMessageStatsAsync(Guid messageId, CancellationToken ct = default);
    Task<List<UserDto>> GetMessageReadersAsync(Guid messageId, CancellationToken ct = default);
    Task<List<UserDto>> GetMessageDeliveredUsersAsync(Guid messageId, CancellationToken ct = default);
    Task<MessageReactionsModel?> ReactToMessageAsync(Guid messageId, ReactionType reactionType, CancellationToken ct = default);
    Task<MessageReactionsModel?> GetMessageReactionsAsync(Guid messageId, CancellationToken ct = default);
    Task<MessageDto?> SendMessageWithReplyAsync(Guid roomId, string content, Guid? replyToMessageId);

}
