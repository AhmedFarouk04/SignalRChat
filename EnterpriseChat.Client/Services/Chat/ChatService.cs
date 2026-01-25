using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Http;
using System.Text;
using System.Text.Json;

namespace EnterpriseChat.Client.Services.Chat;

public sealed class ChatService : IChatService
{
    private readonly IApiClient _api;

    public ChatService(IApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<MessageModel>> GetMessagesAsync(Guid roomId, int skip = 0, int take = 50)
        => await _api.GetAsync<IReadOnlyList<MessageModel>>(ApiEndpoints.RoomMessages(roomId, skip, take)) ?? [];

    public Task<MessageDto?> SendMessageAsync(Guid roomId, string content)
        => _api.PostAsync<object, MessageDto>(ApiEndpoints.SendMessage, new
        {
            RoomId = roomId,
            Content = content
        });

    public async Task<IReadOnlyList<MessageReadReceiptDto>> GetReadersAsync(Guid messageId)
        => await _api.GetAsync<IReadOnlyList<MessageReadReceiptDto>>(ApiEndpoints.MessageReaders(messageId)) ?? [];

    public Task<GroupMembersDto?> GetGroupMembersAsync(Guid roomId)
        => _api.GetAsync<GroupMembersDto>(ApiEndpoints.GroupMembers(roomId));

    public Task MuteAsync(Guid roomId)
        => _api.PostAsync(ApiEndpoints.Mute(roomId));

    public Task UnmuteAsync(Guid roomId)
        => _api.DeleteAsync(ApiEndpoints.Mute(roomId));

    public Task BlockUserAsync(Guid userId)
        => _api.PostAsync(ApiEndpoints.Block(userId));

    public Task RemoveMemberAsync(Guid roomId, Guid userId)
        => _api.DeleteAsync(ApiEndpoints.RemoveGroupMember(roomId, userId));

    public Task MarkMessageDeliveredAsync(Guid messageId)
        => _api.PostAsync(ApiEndpoints.MarkMessageDelivered(messageId));

    public Task MarkMessageReadAsync(Guid messageId)
        => _api.PostAsync(ApiEndpoints.MarkMessageRead(messageId));

    public async Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId)
    {
        var json = JsonSerializer.Serialize(new { LastMessageId = lastMessageId });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        await _api.SendAsync(HttpMethod.Post, ApiEndpoints.MarkRoomRead(roomId), content);
    }

}
