using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseChat.API.Controllers;

[Authorize]
[ApiController]
[Route("api/presence")]
public sealed class PresenceController : BaseController
{
    private readonly IPresenceService _presence;

    public PresenceController(IPresenceService presence)
    {
        _presence = presence;
    }

    [HttpGet("online/{userId:guid}")]
    public async Task<IActionResult> IsOnline(Guid userId)
    {
        if (userId == Guid.Empty) return BadRequest("UserId is required.");
        var online = await _presence.IsOnlineAsync(new UserId(userId));
        return Ok(new { userId, online });
    }

    [HttpGet("online")]
    public async Task<IActionResult> GetOnlineUsers()
    {
        var users = await _presence.GetOnlineUsersAsync();
        return Ok(users.Select(x => x.Value).ToList());
    }
}
