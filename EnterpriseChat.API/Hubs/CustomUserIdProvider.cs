using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EnterpriseChat.API.Hubs;

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        // جرب الأنواع الشائعة للـ user ID في JWT
        return connection.User?.FindFirst(claim =>
            claim.Type == "sub" ||
            claim.Type == ClaimTypes.NameIdentifier ||
            claim.Type == "nameid")?.Value;
    }
}