using EnterpriseChat.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseChat.API.Controllers;

[Authorize]
[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserDirectoryService _users;

    public UsersController(IUserDirectoryService users)
    {
        _users = users;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] int take = 20,
        [FromQuery] Guid? excludeUserId = null,  // ✅ أضف هذا
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("query is required.");

        take = Math.Clamp(take, 1, 50);

        var result = await _users.SearchAsync(query.Trim(), excludeUserId, take, ct);  // ✅ تعديل
        return Ok(result);
    }
}