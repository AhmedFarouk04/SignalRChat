using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EnterpriseChat.API.Hubs;

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
                return connection.User?.FindFirst(claim =>
            claim.Type == "sub" ||
            claim.Type == ClaimTypes.NameIdentifier ||
            claim.Type == "nameid")?.Value;
    }
}