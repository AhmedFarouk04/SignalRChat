namespace EnterpriseChat.Domain.Entities;

public sealed class Attachment
{
    public Guid Id { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid UploaderId { get; private set; }

    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }

    public string StoragePath { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private Attachment() { }

    public Attachment(
        Guid id,
        Guid roomId,
        Guid uploaderId,
        string fileName,
        string contentType,
        long size,
        string storagePath)
    {
        Id = id;
        RoomId = roomId;
        UploaderId = uploaderId;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        StoragePath = storagePath;
        CreatedAt = DateTime.UtcNow;
    }
}
