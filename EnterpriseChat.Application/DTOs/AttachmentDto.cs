namespace EnterpriseChat.Application.DTOs;

public sealed record AttachmentDto(
    Guid Id,
    Guid RoomId,
    Guid UploaderId,
    string FileName,
    string ContentType,
    long Size,
    string DownloadUrl,
    DateTime CreatedAt
);
