using EnterpriseChat.Infrastructure.Persistence;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.API.Controllers;

[Authorize]
[ApiController]
[Route("api/attachments")]
public sealed class AttachmentsController : BaseController
{
    private readonly ChatDbContext _db;
    private readonly IRoomAuthorizationService _auth;

    public AttachmentsController(ChatDbContext db, IRoomAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    // GET /api/attachments/{attachmentId}
    [HttpGet("{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid attachmentId, CancellationToken ct)
    {
        if (attachmentId == Guid.Empty) return BadRequest("AttachmentId is required.");

        var entity = await _db.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == attachmentId, ct);

        if (entity is null) return NotFound("Attachment not found.");

        await _auth.EnsureUserIsMemberAsync(new RoomId(entity.RoomId), GetCurrentUserId(), ct);

        var absolute = Path.Combine(
            Directory.GetCurrentDirectory(),
            entity.StoragePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!System.IO.File.Exists(absolute))
            return NotFound("File not found on disk.");

        var bytes = await System.IO.File.ReadAllBytesAsync(absolute, ct);
        return File(bytes, entity.ContentType, entity.FileName);
    }

    // DELETE /api/attachments/{attachmentId}
    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid attachmentId, CancellationToken ct)
    {
        if (attachmentId == Guid.Empty) return BadRequest("AttachmentId is required.");

        var entity = await _db.Attachments
            .FirstOrDefaultAsync(x => x.Id == attachmentId, ct);

        if (entity is null) return NotFound("Attachment not found.");

        var requester = GetCurrentUserId();

        // صلاحية: uploader أو admin/owner
        if (entity.UploaderId != requester.Value)
            await _auth.EnsureUserIsAdminAsync(new RoomId(entity.RoomId), requester, ct);

        var absolute = Path.Combine(
            Directory.GetCurrentDirectory(),
            entity.StoragePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        _db.Attachments.Remove(entity);
        await _db.SaveChangesAsync(ct);

        if (System.IO.File.Exists(absolute))
            System.IO.File.Delete(absolute);

        return NoContent();
    }
}
