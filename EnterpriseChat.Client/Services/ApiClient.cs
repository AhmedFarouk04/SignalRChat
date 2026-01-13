using EnterpriseChat.Client.Authentication;
using EnterpriseChat.Client.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseChat.Application.DTOs;
namespace EnterpriseChat.Client.Services
{

    public sealed class ApiClient
    {
        private readonly HttpClient _http;
        private readonly ITokenService _tokenService;

        public ApiClient(HttpClient http, ITokenService tokenService)
        {
            _http = http;
            _tokenService = tokenService;
        }

        private async Task AttachTokenAsync()
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IReadOnlyList<RoomModel>> GetRoomsAsync()
        {
            await AttachTokenAsync();
            return await _http.GetFromJsonAsync<IReadOnlyList<RoomModel>>(
                "api/chat/rooms") ?? [];
        }

        public async Task<IReadOnlyList<MessageModel>> GetMessagesAsync(Guid roomId)
        {
            await AttachTokenAsync();
            return await _http.GetFromJsonAsync<IReadOnlyList<MessageModel>>(
                $"api/chat/rooms/{roomId}/messages") ?? [];
        }


        public async Task<IReadOnlyList<MessageReadReceiptDto>> GetReadersAsync(
    Guid messageId)
        {
            await AttachTokenAsync();

            return await _http.GetFromJsonAsync<
                IReadOnlyList<MessageReadReceiptDto>>(
                $"api/chat/messages/{messageId}/readers") ?? [];
        }

    }

}