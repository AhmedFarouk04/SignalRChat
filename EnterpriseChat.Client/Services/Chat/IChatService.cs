using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Models;

namespace EnterpriseChat.Client.Services.Chat;

public interface IChatService
{
    Task<IReadOnlyList<MessageModel>> GetMessagesAsync(Guid roomId, int skip = 0, int take = 50);
    Task<MessageDto?> SendMessageAsync(Guid roomId, string content);
    Task<IReadOnlyList<MessageReadReceiptDto>> GetReadersAsync(Guid messageId);

    Task<GroupMembersDto?> GetGroupMembersAsync(Guid roomId);

    Task MuteAsync(Guid roomId);
    Task UnmuteAsync(Guid roomId);
    Task BlockUserAsync(Guid userId);
    Task RemoveMemberAsync(Guid roomId, Guid userId);
    Task MarkMessageDeliveredAsync(Guid messageId);
    Task MarkMessageReadAsync(Guid messageId);
    Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId);

}
