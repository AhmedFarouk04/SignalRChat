using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Http;
using EnterpriseChat.Domain.Enums;
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
    {
        var dtos = await _api.GetAsync<IReadOnlyList<MessageReadDto>>(ApiEndpoints.RoomMessages(roomId, skip, take))
                   ?? [];

        var ordered = dtos.OrderBy(m => m.CreatedAt);

        return ordered.Select(m => new MessageModel
        {
            Id = m.Id,
            RoomId = m.RoomId,
            SenderId = m.SenderId,
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            Status = (Models.MessageStatus)(int)m.Status,
            Receipts = (m.Receipts ?? new()).Select(r => new MessageReceiptModel
            {
                UserId = r.UserId,
                Status = (Models.MessageStatus)(int)m.Status,
            }).ToList()
        }).ToList();
    }

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

    public async Task<MessageReceiptStatsDto?> GetMessageStatsAsync(Guid messageId, CancellationToken ct = default)
    {
        return await _api.GetAsync<MessageReceiptStatsDto>($"/api/chat/messages/{messageId}/stats", ct);
    }

    public async Task<List<UserDto>> GetMessageReadersAsync(Guid messageId, CancellationToken ct = default)
    {
        var readers = await _api.GetAsync<List<UserDto>>($"/api/chat/messages/{messageId}/readers-details", ct);
        return readers ?? new List<UserDto>();
    }

    public async Task<List<UserDto>> GetMessageDeliveredUsersAsync(Guid messageId, CancellationToken ct = default)
    {
        var delivered = await _api.GetAsync<List<UserDto>>($"/api/chat/messages/{messageId}/delivered-details", ct);
        return delivered ?? new List<UserDto>();
    }

    public async Task StartTypingAsync(Guid roomId, int ttlSeconds = 5, CancellationToken ct = default)
    {
        var request = new StartTypingRequest { TtlSeconds = ttlSeconds };
        await _api.PostAsync<StartTypingRequest, object>(
            ApiEndpoints.StartTyping(roomId),
            request,
            ct);
    }

    public async Task StopTypingAsync(Guid roomId, CancellationToken ct = default)
    {
        await _api.PostAsync(ApiEndpoints.StopTyping(roomId), ct: ct);
    }
    public async Task<MessageReactionsModel?> ReactToMessageAsync(Guid messageId, ReactionType reactionType, CancellationToken ct = default)
    {
        var request = new { ReactionType = reactionType };
        return await _api.PostAsync<object, MessageReactionsModel>(
            $"/api/chat/messages/{messageId}/react",
            request,
            ct);
    }

    public async Task<MessageReactionsModel?> GetMessageReactionsAsync(Guid messageId, CancellationToken ct = default)
    {
        return await _api.GetAsync<MessageReactionsModel>($"/api/chat/messages/{messageId}/reactions", ct);
    }
    // EnterpriseChat.Client/Services/ChatService.cs
    // EnterpriseChat.Client/Services/ChatService.cs
    public async Task<MessageDto?> SendMessageWithReplyAsync(Guid roomId, string content, Guid? replyToMessageId)
    {
        var request = new
        {
            RoomId = roomId,
            Content = content,
            ReplyToMessageId = replyToMessageId
        };

        return await _api.PostAsync<object, MessageDto>(
            ApiEndpoints.SendMessage,
            request);
    }
}
