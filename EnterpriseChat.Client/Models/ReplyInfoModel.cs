// EnterpriseChat.Client/Models/ReplyInfoModel.cs
namespace EnterpriseChat.Client.Models;

public class ReplyInfoModel
{
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}