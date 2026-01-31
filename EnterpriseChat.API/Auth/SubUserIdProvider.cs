using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EnterpriseChat.API.Auth;

public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst("sub")?.Value
            ?? connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("nameid")?.Value;
    }
}
