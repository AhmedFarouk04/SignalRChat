namespace EnterpriseChat.Client.Models;

public sealed class ReplyContext
{
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string ContentPreview { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public void Clear()
    {
        MessageId = Guid.Empty;
        SenderId = Guid.Empty;
        SenderName = "";
        ContentPreview = "";
        CreatedAt = default;
    }
}