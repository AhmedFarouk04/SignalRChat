using System.Net.Http.Json;
using EnterpriseChat.Application.DTOs;

namespace EnterpriseChat.Client.Services.Reaction;

public sealed class ReactionsApi
{
    private readonly HttpClient _http;

    public ReactionsApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<MessageReactionsDetailsDto> GetReactionDetails(Guid messageId)
    {
        return await _http.GetFromJsonAsync<MessageReactionsDetailsDto>(
            $"messages/{messageId}/reactions/details")!;
    }

    public async Task RemoveMyReaction(Guid messageId)
    {
        await _http.DeleteAsync($"messages/{messageId}/reactions/me");
    }
}
