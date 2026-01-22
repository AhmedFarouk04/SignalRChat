using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IAttachmentService
{
    Task<AttachmentDto> UploadAsync(
        RoomId roomId,
        UserId uploaderId,
        Stream content,
        string fileName,
        string contentType,
        long size,
        CancellationToken ct = default);
}
