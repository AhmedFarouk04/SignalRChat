using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Services;

public sealed class LocalAttachmentService : IAttachmentService
{
    private readonly ChatDbContext _db;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;

    public LocalAttachmentService(ChatDbContext db, IRoomAuthorizationService auth, IUnitOfWork uow)
    {
        _db = db;
        _auth = auth;
        _uow = uow;
    }

    public async Task<AttachmentDto> UploadAsync(
        RoomId roomId,
        UserId uploaderId,
        Stream content,
        string fileName,
        string contentType,
        long size,
        CancellationToken ct = default)
    {
        await _auth.EnsureUserIsMemberAsync(roomId, uploaderId, ct);

        if (size <= 0) throw new ArgumentException("File is empty.");
        if (size > 25_000_000) throw new ArgumentException("File too large.");

        var safeName = Path.GetFileName(fileName);
        var ext = Path.GetExtension(safeName).ToLowerInvariant();

        var allowed = new HashSet<string> { ".png", ".jpg", ".jpeg", ".pdf", ".txt", ".docx", ".xlsx" };
        if (!allowed.Contains(ext))
            throw new ArgumentException("File type not allowed.");

        var id = Guid.NewGuid();

        var relative = Path.Combine("App_Data", "uploads", roomId.Value.ToString(), $"{id}{ext}")
            .Replace("\\", "/");
        var absolute = Path.Combine(Directory.GetCurrentDirectory(),
            relative.Replace("/", Path.DirectorySeparatorChar.ToString()));

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        await using (var fs = File.Create(absolute))
        {
            await content.CopyToAsync(fs, ct);
        }

        var entity = new Attachment(
            id: id,
            roomId: roomId.Value,
            uploaderId: uploaderId.Value,
            fileName: safeName,
            contentType: string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            size: size,
            storagePath: relative);

        _db.Attachments.Add(entity);
        await _uow.CommitAsync(ct);

        return new AttachmentDto(
            Id: entity.Id,
            RoomId: entity.RoomId,
            UploaderId: entity.UploaderId,
            FileName: entity.FileName,
            ContentType: entity.ContentType,
            Size: entity.Size,
            DownloadUrl: $"/api/attachments/{entity.Id}",
            CreatedAt: entity.CreatedAt
        );
    }
}
